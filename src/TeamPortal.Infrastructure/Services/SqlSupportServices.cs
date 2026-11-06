using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Infrastructure.Data;
using AppEnums = TeamPortal.Application.Enums;
using DomainEnums = TeamPortal.Domain.Enums;

namespace TeamPortal.Infrastructure.Services;

internal sealed class SqlActivityLogService : SqlServiceBase, IActivityLogService
{
    public SqlActivityLogService(
        IDbContextFactory<PortalDbContext> factory, ICurrentUserService currentUser)
        : base(factory, currentUser)
    {
    }

    public async Task<PagedResult<ActivityLogEntryDto>> GetForEntityAsync(
        string entityName, int entityId, int page = 1, int pageSize = 25,
        CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        string needle = (entityName ?? string.Empty).ToUpperInvariant();

        return await PageAsync(
            db.ActivityLogs.Where(a => a.EntityName.ToUpper() == needle && a.EntityId == entityId),
            page, pageSize, ct);
    }

    public async Task<PagedResult<ActivityLogEntryDto>> GetRecentAsync(
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await PageAsync(db.ActivityLogs, page, pageSize, ct);
    }

    public async Task WriteAsync(
        AppEnums.ActivityAction action, string entityName, int? entityId, string summary,
        CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        WriteActivity(db, (DomainEnums.ActivityAction)action, entityName, entityId, summary);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Newest first, then paged. The log only grows, so an unpaged read would get
    /// slower every day the app is used.
    /// </summary>
    private static async Task<PagedResult<ActivityLogEntryDto>> PageAsync(
        IQueryable<Domain.Entities.ActivityLog> source, int page, int pageSize, CancellationToken ct)
    {
        int safePage = Math.Max(1, page);
        int safeSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Domain.Entities.ActivityLog> ordered = source
            .AsNoTracking()
            .OrderByDescending(a => a.HappenedAt)
            .ThenByDescending(a => a.Id);

        int total = await ordered.CountAsync(ct);

        List<ActivityLogEntryDto> items = await ordered
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .Select(a => new ActivityLogEntryDto(
                a.Id,
                a.User == null ? null : a.User.FullName,
                (AppEnums.ActivityAction)a.Action,
                a.EntityName,
                a.EntityId,
                a.Summary,
                a.HappenedAt))
            .ToListAsync(ct);

        return new PagedResult<ActivityLogEntryDto>(items, total, safePage, safeSize);
    }
}

internal sealed class SqlNotificationService : SqlServiceBase, INotificationService
{
    public SqlNotificationService(
        IDbContextFactory<PortalDbContext> factory, ICurrentUserService currentUser)
        : base(factory, currentUser)
    {
    }

    public async Task<IReadOnlyList<NotificationDto>> GetForCurrentUserAsync(
        bool unreadOnly = false, CancellationToken ct = default)
    {
        if (CurrentUser.UserId is not int userId)
        {
            return [];
        }

        await using PortalDbContext db = await OpenAsync(ct);

        return await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Select(n => new NotificationDto(
                n.Id, n.Title, n.Message, n.LinkUrl, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        if (CurrentUser.UserId is not int userId)
        {
            return 0;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        return await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task<Result> MarkAsReadAsync(int notificationId, CancellationToken ct = default)
    {
        if (CurrentUser.UserId is not int userId)
        {
            return Result.NotFound("That notification");
        }

        await using PortalDbContext db = await OpenAsync(ct);

        // Scoped to the signed-in user's own rows. Without the UserId check anyone
        // could mark - and therefore discover - someone else's alerts.
        Domain.Entities.Notification? notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification is null)
        {
            return Result.NotFound("That notification");
        }

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(CancellationToken ct = default)
    {
        if (CurrentUser.UserId is not int userId)
        {
            return Result.Success();
        }

        await using PortalDbContext db = await OpenAsync(ct);

        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        return Result.Success();
    }
}

internal sealed class SqlStepStatusService : SqlServiceBase, IStepStatusService
{
    public SqlStepStatusService(
        IDbContextFactory<PortalDbContext> factory, ICurrentUserService currentUser)
        : base(factory, currentUser)
    {
    }

    public async Task<IReadOnlyList<StepStatusDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await db.StepStatuses
            .AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .Select(s => new StepStatusDto(s.Id, s.Name, s.ColourHex, s.IsDefault))
            .ToListAsync(ct);
    }
}
