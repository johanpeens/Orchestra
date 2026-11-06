using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Domain.Identity;
using TeamPortal.Infrastructure.Data;
using AppEnums = TeamPortal.Application.Enums;
using DomainEnums = TeamPortal.Domain.Enums;

namespace TeamPortal.Infrastructure.Identity;

internal sealed class SqlUserApprovalService : IUserApprovalService
{
    private readonly IDbContextFactory<PortalDbContext> _factory;
    private readonly UserManager<AppUser> _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailSender _email;
    private readonly IAppUrlProvider _urls;
    private readonly IActivityLogService _activityLog;

    public SqlUserApprovalService(
        IDbContextFactory<PortalDbContext> factory,
        UserManager<AppUser> users,
        ICurrentUserService currentUser,
        IEmailSender email,
        IAppUrlProvider urls,
        IActivityLogService activityLog)
    {
        _factory = factory;
        _users = users;
        _currentUser = currentUser;
        _email = email;
        _urls = urls;
        _activityLog = activityLog;
    }

    public async Task<IReadOnlyList<UserAccountDto>> GetAllAsync(
        AppEnums.ApprovalFilter filter = AppEnums.ApprovalFilter.All,
        CancellationToken ct = default)
    {
        if (!IsAdmin)
        {
            return [];
        }

        await using PortalDbContext db = await _factory.CreateDbContextAsync(ct);

        // One query with the roles joined in, not one per row. The grid shows
        // every account, and an N+1 here scales with the whole user table.
        var rows = await db.Users
            .AsNoTracking()
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.EmailConfirmed,
                u.ApprovalStatus,
                u.RegisteredAt,
                u.ApprovalDecidedAt,
                u.ApprovalDecidedByUserId,
                u.LastLoginAt,
                Roles = db.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                    .ToList(),
                DecidedBy = db.Users
                    .Where(a => a.Id == u.ApprovalDecidedByUserId)
                    .Select(a => a.FullName)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        IEnumerable<UserAccountDto> accounts = rows.Select(r => new UserAccountDto(
            r.Id,
            r.FullName,
            r.Email ?? string.Empty,
            r.EmailConfirmed,
            (AppEnums.ApprovalStatus)r.ApprovalStatus,
            r.Roles,
            r.RegisteredAt,
            r.ApprovalDecidedAt,
            r.DecidedBy,
            r.LastLoginAt));

        accounts = filter switch
        {
            AppEnums.ApprovalFilter.AwaitingDecision => accounts.Where(a => a.IsAwaitingDecision),
            AppEnums.ApprovalFilter.Approved =>
                accounts.Where(a => a.ApprovalStatus == AppEnums.ApprovalStatus.Approved),
            AppEnums.ApprovalFilter.Rejected =>
                accounts.Where(a => a.ApprovalStatus == AppEnums.ApprovalStatus.Rejected),
            _ => accounts
        };

        // Waiting first - the grid exists to clear that queue. Then newest.
        return
        [
            .. accounts
                .OrderByDescending(a => a.IsAwaitingDecision)
                .ThenByDescending(a => a.RegisteredAt ?? DateTime.MinValue)
                .ThenBy(a => a.FullName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        if (!IsAdmin)
        {
            return 0;
        }

        await using PortalDbContext db = await _factory.CreateDbContextAsync(ct);

        return await db.Users.CountAsync(
            u => u.EmailConfirmed && u.ApprovalStatus == DomainEnums.ApprovalStatus.Pending, ct);
    }

    public async Task<Result> ApproveAsync(
        int userId, string role, CancellationToken ct = default)
    {
        if (RequireAdmin() is { } denied)
        {
            return denied;
        }

        if (!AppRoles.All.Contains(role, StringComparer.Ordinal))
        {
            return Result.Invalid(nameof(role), "That is not a role this portal has.");
        }

        AppUser? user = await _users.FindByIdAsync(userId.ToString(Culture));

        if (user is null)
        {
            return Result.NotFound("That account");
        }

        // The first gate has to be through before the second one opens. Approving
        // an address nobody has proved they can read would skip it entirely.
        if (!user.EmailConfirmed)
        {
            return Result.Invalid(
                ValidationError.FormLevel,
                $"{user.FullName} has not confirmed their email address yet. " +
                "They have to click the link before they can be approved.");
        }

        user.ApprovalStatus = DomainEnums.ApprovalStatus.Approved;
        user.ApprovalDecidedAt = DateTime.UtcNow;
        user.ApprovalDecidedByUserId = _currentUser.UserId;

        IdentityResult updated = await _users.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            return Result.Invalid(
                ValidationError.FormLevel, "That account could not be updated. Try again.");
        }

        await SetSoleRoleAsync(user, role);

        await _email.SendAsync(
            EmailBodies.AccountApproved(
                user.Email ?? string.Empty, user.FullName, _urls.Absolute("login")),
            ct);

        await _activityLog.WriteAsync(
            AppEnums.ActivityAction.Updated, "AppUser", user.Id,
            $"Approved {user.FullName} as {role}", ct);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(int userId, CancellationToken ct = default)
    {
        if (RequireAdmin() is { } denied)
        {
            return denied;
        }

        AppUser? user = await _users.FindByIdAsync(userId.ToString(Culture));

        if (user is null)
        {
            return Result.NotFound("That account");
        }

        if (user.Id == _currentUser.UserId)
        {
            return Result.Invalid(
                ValidationError.FormLevel, "You cannot reject your own account.");
        }

        user.ApprovalStatus = DomainEnums.ApprovalStatus.Rejected;
        user.ApprovalDecidedAt = DateTime.UtcNow;
        user.ApprovalDecidedByUserId = _currentUser.UserId;

        await _users.UpdateAsync(user);

        // Every role removed. A rejected account that kept a role would come back
        // to life the moment somebody flipped its status by hand.
        await SetSoleRoleAsync(user, role: null);

        // No email. Telling a stranger their request was seen and refused is more
        // information than they are owed.
        await _activityLog.WriteAsync(
            AppEnums.ActivityAction.Updated, "AppUser", user.Id,
            $"Rejected the registration for {user.Email}", ct);

        return Result.Success();
    }

    public async Task<Result> ChangeRoleAsync(
        int userId, string role, CancellationToken ct = default)
    {
        if (RequireAdmin() is { } denied)
        {
            return denied;
        }

        if (!AppRoles.All.Contains(role, StringComparer.Ordinal))
        {
            return Result.Invalid(nameof(role), "That is not a role this portal has.");
        }

        AppUser? user = await _users.FindByIdAsync(userId.ToString(Culture));

        if (user is null)
        {
            return Result.NotFound("That account");
        }

        // An administrator demoting themselves locks everyone out of this screen
        // if they are the last one. Refused rather than warned about.
        if (user.Id == _currentUser.UserId && role != AppRoles.Admin)
        {
            return Result.Invalid(
                ValidationError.FormLevel,
                "You cannot take the Admin role away from yourself. Ask another administrator.");
        }

        await SetSoleRoleAsync(user, role);

        await _activityLog.WriteAsync(
            AppEnums.ActivityAction.Updated, "AppUser", user.Id,
            $"Changed {user.FullName} to {role}", ct);

        return Result.Success();
    }

    private static System.Globalization.CultureInfo Culture =>
        System.Globalization.CultureInfo.InvariantCulture;

    private bool IsAdmin => _currentUser.IsSignedIn && _currentUser.IsInRole(AppRoles.Admin);

    private Result? RequireAdmin() =>
        IsAdmin
            ? null
            : Result.Invalid(
                ValidationError.FormLevel,
                "You do not have permission to do that. Ask an administrator if you need it.");

    /// <summary>
    /// Replaces every role with the one given, or removes them all when null.
    /// This portal gives one role per person; leaving an old one behind is how
    /// somebody ends up quietly still an editor after being moved to Viewer.
    /// </summary>
    private async Task SetSoleRoleAsync(AppUser user, string? role)
    {
        IList<string> current = await _users.GetRolesAsync(user);

        if (current.Count > 0)
        {
            await _users.RemoveFromRolesAsync(user, current);
        }

        if (role is not null)
        {
            await _users.AddToRoleAsync(user, role);
        }
    }
}
