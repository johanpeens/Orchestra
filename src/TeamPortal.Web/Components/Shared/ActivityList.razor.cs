using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;

namespace TeamPortal.Web.Components.Shared;

public partial class ActivityList : ComponentBase
{
    private PagedResult<ActivityLogEntryDto> _page = PagedResult<ActivityLogEntryDto>.Empty(25);
    private bool _loading = true;
    private int _currentPage = 1;

    [Inject]
    private IActivityLogService ActivityLog { get; set; } = null!;

    /// <summary>
    /// Scope the list to one record, e.g. "Process" 1. Leave null for the
    /// whole-app log.
    /// </summary>
    [Parameter]
    public string? EntityName { get; set; }

    [Parameter]
    public int? EntityId { get; set; }

    [Parameter]
    public int PageSize { get; set; } = 25;

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _page = EntityName is not null && EntityId is int id
                ? await ActivityLog.GetForEntityAsync(EntityName, id, _currentPage, PageSize)
                : await ActivityLog.GetRecentAsync(_currentPage, PageSize);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task PreviousAsync()
    {
        _currentPage = Math.Max(1, _currentPage - 1);
        await LoadAsync();
    }

    private async Task NextAsync()
    {
        _currentPage++;
        await LoadAsync();
    }

    private static string IconFor(ActivityAction action) => action switch
    {
        ActivityAction.Created => Icons.Material.Outlined.AddCircle,
        ActivityAction.Updated => Icons.Material.Outlined.Edit,
        ActivityAction.Deleted => Icons.Material.Outlined.Delete,
        ActivityAction.Uploaded => Icons.Material.Outlined.CloudUpload,
        ActivityAction.LoggedIn => Icons.Material.Outlined.Login,
        ActivityAction.LoggedOut => Icons.Material.Outlined.Logout,
        _ => Icons.Material.Outlined.Visibility
    };
}
