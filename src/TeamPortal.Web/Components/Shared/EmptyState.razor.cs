using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TeamPortal.Web.Components.Shared;

public partial class EmptyState : ComponentBase
{
    /// <summary>What is missing, e.g. "No employees yet".</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional second line telling the user what to do about it.</summary>
    [Parameter]
    public string? Description { get; set; }

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Outlined.Inbox;

    /// <summary>Optional call to action, usually the same Add button as the toolbar.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
