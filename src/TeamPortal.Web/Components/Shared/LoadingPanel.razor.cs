using Microsoft.AspNetCore.Components;

namespace TeamPortal.Web.Components.Shared;

public partial class LoadingPanel : ComponentBase
{
    /// <summary>Whether the panel is shown. Bind this to the page's busy flag.</summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Announced to screen readers via aria-live, so the wait is not silent for
    /// anyone who cannot see the spinner.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = "Loading...";
}
