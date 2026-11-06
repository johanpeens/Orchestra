using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Web.Components.Layout;

public partial class NotificationBell : ComponentBase
{
    private IReadOnlyList<NotificationDto> _notifications = [];
    private int _unreadCount;

    [Inject]
    private INotificationService Notifications { get; set; } = null!;

    /// <summary>
    /// The accessible name changes with the count, so a screen reader announces
    /// "3 unread notifications" rather than just "button".
    /// </summary>
    private string BellLabel => _unreadCount switch
    {
        0 => "Notifications, none unread",
        1 => "Notifications, 1 unread",
        _ => $"Notifications, {_unreadCount} unread"
    };

    protected override async Task OnInitializedAsync() => await RefreshAsync();

    private async Task RefreshAsync()
    {
        _notifications = await Notifications.GetForCurrentUserAsync();
        _unreadCount = await Notifications.GetUnreadCountAsync();
    }

    private async Task MarkReadAsync(int id)
    {
        await Notifications.MarkAsReadAsync(id);
        await RefreshAsync();
    }

    private async Task MarkAllReadAsync()
    {
        await Notifications.MarkAllAsReadAsync();
        await RefreshAsync();
    }
}
