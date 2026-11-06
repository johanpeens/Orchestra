using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;

namespace TeamPortal.Mock.Services;

internal sealed class MockUserApprovalService : MockServiceBase, IUserApprovalService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailSender _email;
    private readonly IAppUrlProvider _urls;

    public MockUserApprovalService(
        MockDataStore store,
        MockOptions options,
        ICurrentUserService currentUser,
        IEmailSender email,
        IAppUrlProvider urls)
        : base(store, options)
    {
        _currentUser = currentUser;
        _email = email;
        _urls = urls;
    }

    public async Task<IReadOnlyList<UserAccountDto>> GetAllAsync(
        ApprovalFilter filter = ApprovalFilter.All, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        if (!IsAdmin)
        {
            return [];
        }

        lock (Store.Gate)
        {
            IEnumerable<UserAccountDto> accounts = Store.Users.Select(Project);

            accounts = filter switch
            {
                ApprovalFilter.AwaitingDecision => accounts.Where(a => a.IsAwaitingDecision),
                ApprovalFilter.Approved =>
                    accounts.Where(a => a.ApprovalStatus == ApprovalStatus.Approved),
                ApprovalFilter.Rejected =>
                    accounts.Where(a => a.ApprovalStatus == ApprovalStatus.Rejected),
                _ => accounts
            };

            return
            [
                .. accounts
                    .OrderByDescending(a => a.IsAwaitingDecision)
                    .ThenByDescending(a => a.RegisteredAt ?? DateTime.MinValue)
                    .ThenBy(a => a.FullName, StringComparer.OrdinalIgnoreCase)
            ];
        }
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        if (!IsAdmin)
        {
            return 0;
        }

        lock (Store.Gate)
        {
            return Store.Users.Count(
                u => u.EmailConfirmed && u.ApprovalStatus == ApprovalStatus.Pending);
        }
    }

    public async Task<Result> ApproveAsync(
        int userId, string role, CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        if (RequireAdmin() is { } denied)
        {
            return denied;
        }

        if (!AppRoles.All.Contains(role, StringComparer.Ordinal))
        {
            return Result.Invalid(nameof(role), "That is not a role this portal has.");
        }

        string email;
        string fullName;

        lock (Store.Gate)
        {
            MockUser? user = Store.Users.FirstOrDefault(u => u.Id == userId);

            if (user is null)
            {
                return Result.NotFound("That account");
            }

            // The first gate has to be through before the second one opens.
            if (!user.EmailConfirmed)
            {
                return Result.Invalid(
                    ValidationError.FormLevel,
                    $"{user.FullName} has not confirmed their email address yet. " +
                    "They have to click the link before they can be approved.");
            }

            user.ApprovalStatus = ApprovalStatus.Approved;
            user.ApprovalDecidedAt = DateTime.UtcNow;
            user.ApprovalDecidedByUserId = _currentUser.UserId;
            user.Roles = [role];

            email = user.Email;
            fullName = user.FullName;

            Store.WriteActivity(
                ActivityAction.Updated, "AppUser", user.Id,
                $"Approved {user.FullName} as {role}", _currentUser.UserId);
        }

        await _email.SendAsync(
            EmailBodies.AccountApproved(email, fullName, _urls.Absolute("login")), ct);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(int userId, CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        if (RequireAdmin() is { } denied)
        {
            return denied;
        }

        lock (Store.Gate)
        {
            MockUser? user = Store.Users.FirstOrDefault(u => u.Id == userId);

            if (user is null)
            {
                return Result.NotFound("That account");
            }

            if (user.Id == _currentUser.UserId)
            {
                return Result.Invalid(
                    ValidationError.FormLevel, "You cannot reject your own account.");
            }

            user.ApprovalStatus = ApprovalStatus.Rejected;
            user.ApprovalDecidedAt = DateTime.UtcNow;
            user.ApprovalDecidedByUserId = _currentUser.UserId;

            // Every role removed, so flipping the status back by hand does not
            // quietly restore what they had.
            user.Roles = [];

            Store.WriteActivity(
                ActivityAction.Updated, "AppUser", user.Id,
                $"Rejected the registration for {user.Email}", _currentUser.UserId);

            return Result.Success();
        }
    }

    public async Task<Result> ChangeRoleAsync(
        int userId, string role, CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        if (RequireAdmin() is { } denied)
        {
            return denied;
        }

        if (!AppRoles.All.Contains(role, StringComparer.Ordinal))
        {
            return Result.Invalid(nameof(role), "That is not a role this portal has.");
        }

        lock (Store.Gate)
        {
            MockUser? user = Store.Users.FirstOrDefault(u => u.Id == userId);

            if (user is null)
            {
                return Result.NotFound("That account");
            }

            if (user.Id == _currentUser.UserId && role != AppRoles.Admin)
            {
                return Result.Invalid(
                    ValidationError.FormLevel,
                    "You cannot take the Admin role away from yourself. Ask another administrator.");
            }

            user.Roles = [role];

            Store.WriteActivity(
                ActivityAction.Updated, "AppUser", user.Id,
                $"Changed {user.FullName} to {role}", _currentUser.UserId);

            return Result.Success();
        }
    }

    private bool IsAdmin => _currentUser.IsSignedIn && _currentUser.IsInRole(AppRoles.Admin);

    private Result? RequireAdmin() =>
        IsAdmin
            ? null
            : Result.Invalid(
                ValidationError.FormLevel,
                "You do not have permission to do that. Ask an administrator if you need it.");

    private UserAccountDto Project(MockUser user) =>
        new(
            user.Id,
            user.FullName,
            user.Email,
            user.EmailConfirmed,
            user.ApprovalStatus,
            [.. user.Roles],
            user.RegisteredAt,
            user.ApprovalDecidedAt,
            user.ApprovalDecidedByUserId is int by
                ? Store.Users.FirstOrDefault(u => u.Id == by)?.FullName
                : null,
            user.LastLoginAt);
}
