using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TeamPortal.Web.Components.Shared;

public partial class AppSidebar : ComponentBase
{
    /// <summary>Small uppercase label, e.g. "PROCESSES" or "ORGANIZATION".</summary>
    [Parameter]
    [EditorRequired]
    public string Heading { get; set; } = string.Empty;

    /// <summary>Text on the inline button beside the heading. Omit to hide it.</summary>
    [Parameter]
    public string? ActionText { get; set; }

    [Parameter]
    public string ActionIcon { get; set; } = Icons.Material.Filled.Add;

    [Parameter]
    public EventCallback OnAction { get; set; }

    /// <summary>Text on the button pinned to the bottom. Omit to hide it.</summary>
    [Parameter]
    public string? FooterText { get; set; }

    [Parameter]
    public string FooterIcon { get; set; } = Icons.Material.Outlined.History;

    [Parameter]
    public EventCallback OnFooter { get; set; }

    /// <summary>The rows between the heading and the footer.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
