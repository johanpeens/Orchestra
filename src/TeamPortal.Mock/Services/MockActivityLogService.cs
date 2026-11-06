using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;

namespace TeamPortal.Mock.Services;

internal sealed class MockActivityLogService : MockServiceBase, IActivityLogService
{
    private readonly ICurrentUserService _currentUser;

    public MockActivityLogService(
        MockDataStore store, MockOptions options, ICurrentUserService currentUser)
        : base(store, options) => _currentUser = currentUser;

    public async Task<PagedResult<ActivityLogEntryDto>> GetForEntityAsync(
        string entityName, int entityId, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return Page(
                Store.ActivityLogs.Where(a =>
                    string.Equals(a.EntityName, entityName, StringComparison.OrdinalIgnoreCase) &&
                    a.EntityId == entityId),
                page,
                pageSize);
        }
    }

    public async Task<PagedResult<ActivityLogEntryDto>> GetRecentAsync(
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return Page(Store.ActivityLogs, page, pageSize);
        }
    }

    public async Task WriteAsync(
        ActivityAction action, string entityName, int? entityId, string summary,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        lock (Store.Gate)
        {
            Store.WriteActivity(action, entityName, entityId, summary, _currentUser.UserId);
        }
    }

    /// <summary>
    /// Newest first, then paged. The log only grows, so an unpaged read would get
    /// slower every day the app is used.
    /// </summary>
    private PagedResult<ActivityLogEntryDto> Page(
        IEnumerable<MockActivityLog> source, int page, int pageSize)
    {
        int safePage = Math.Max(1, page);
        int safeSize = Math.Clamp(pageSize, 1, 200);

        List<MockActivityLog> ordered =
        [
            .. source.OrderByDescending(a => a.HappenedAt).ThenByDescending(a => a.Id)
        ];

        IReadOnlyList<ActivityLogEntryDto> items =
        [
            .. ordered
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .Select(a => new ActivityLogEntryDto(
                    a.Id,
                    a.UserId is null
                        ? null
                        : Store.Users.FirstOrDefault(u => u.Id == a.UserId)?.FullName,
                    a.Action,
                    a.EntityName,
                    a.EntityId,
                    a.Summary,
                    a.HappenedAt))
        ];

        return new PagedResult<ActivityLogEntryDto>(items, ordered.Count, safePage, safeSize);
    }
}

internal sealed class MockNotificationService : MockServiceBase, INotificationService
{
    private readonly ICurrentUserService _currentUser;

    public MockNotificationService(
        MockDataStore store, MockOptions options, ICurrentUserService currentUser)
        : base(store, options) => _currentUser = currentUser;

    public async Task<IReadOnlyList<NotificationDto>> GetForCurrentUserAsync(
        bool unreadOnly = false, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        if (_currentUser.UserId is not int userId)
        {
            return [];
        }

        lock (Store.Gate)
        {
            return
            [
                .. Store.Notifications
                    .Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
                    .OrderByDescending(n => n.CreatedAt)
                    .ThenByDescending(n => n.Id)
                    .Select(n => new NotificationDto(
                        n.Id, n.Title, n.Message, n.LinkUrl, n.IsRead, n.CreatedAt))
            ];
        }
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        if (_currentUser.UserId is not int userId)
        {
            return 0;
        }

        lock (Store.Gate)
        {
            return Store.Notifications.Count(n => n.UserId == userId && !n.IsRead);
        }
    }

    public async Task<Result> MarkAsReadAsync(int notificationId, CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        if (_currentUser.UserId is not int userId)
        {
            return Result.NotFound("That notification");
        }

        lock (Store.Gate)
        {
            // Scoped to the current user's own rows. Without the UserId check
            // anyone could mark - and therefore discover - someone else's alerts.
            MockNotification? notification = Store.Notifications
                .FirstOrDefault(n => n.Id == notificationId && n.UserId == userId);

            if (notification is null)
            {
                return Result.NotFound("That notification");
            }

            notification.IsRead = true;
            return Result.Success();
        }
    }

    public async Task<Result> MarkAllAsReadAsync(CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        if (_currentUser.UserId is not int userId)
        {
            return Result.Success();
        }

        lock (Store.Gate)
        {
            foreach (MockNotification n in Store.Notifications.Where(n => n.UserId == userId))
            {
                n.IsRead = true;
            }

            return Result.Success();
        }
    }
}
