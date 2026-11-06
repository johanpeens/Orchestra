using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TeamPortal.Web.Components.Shared;

public partial class SidebarItem : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string Href { get; set; } = "#";

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Outlined.Folder;

    /// <summary>The badge on the right. Null hides it.</summary>
    [Parameter]
    public int? Count { get; set; }

    [Parameter]
    public bool Selected { get; set; }

    /// <summary>
    /// Shows a delete button on the row. Left unset the button is not rendered
    /// at all, so a sidebar whose rows cannot be deleted looks exactly as it did.
    /// </summary>
    [Parameter]
    public EventCallback OnDelete { get; set; }

    private string RowClass =>
        Selected ? "app-sidebar__row app-sidebar__row--selected" : "app-sidebar__row";
}
