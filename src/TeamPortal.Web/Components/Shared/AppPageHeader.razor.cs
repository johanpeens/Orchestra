using Microsoft.AspNetCore.Components;

namespace TeamPortal.Web.Components.Shared;

public partial class AppPageHeader : ComponentBase
{
    /// <summary>
    /// The small caption above the title, e.g. "Welcome To Your", "You are viewing".
    /// Optional - docs/design/02 and docs/design/05 have a description line instead.
    /// </summary>
    [Parameter]
    public string? Overline { get; set; }

    /// <summary>The page name, e.g. "Dashboard", "Clients". Required.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The sentence under the title, e.g. "Manage and view members, roles, and
    /// business enablement structures." Optional.
    /// </summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>Optional right-aligned slot for toolbar buttons or status chips.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void OnParametersSet()
    {
        // [EditorRequired] is a compile-time hint only - it warns in the IDE and
        // does nothing at runtime. A page with no name is always a bug, so fail
        // loudly in a test rather than render a blank banner in production.
        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException(
                $"{nameof(AppPageHeader)} requires a {nameof(Title)}.", nameof(Title));
        }
    }
}
