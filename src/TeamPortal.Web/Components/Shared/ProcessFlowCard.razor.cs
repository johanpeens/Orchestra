using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Web.Components.Shared;

public partial class ProcessFlowCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ProcessListItemDto Process { get; set; } = null!;

    /// <summary>
    /// Position in the strip. Passed in rather than read from SortOrder so the
    /// badges always read 1..n even if the stored order has a gap.
    /// </summary>
    [Parameter]
    public int Number { get; set; }

    [Parameter]
    public bool Selected { get; set; }

    [Parameter]
    public EventCallback<int> OnSelect { get; set; }

    /// <summary>Double-click. Opens the whole process, steps and all.</summary>
    [Parameter]
    public EventCallback<int> OnOpenDetail { get; set; }

    /// <summary>
    /// Raised on hover and on keyboard focus. The page loads the preview the
    /// first time and caches it - a strip of seven processes should not cost
    /// seven round trips before anybody has pointed at one.
    /// </summary>
    [Parameter]
    public EventCallback<PreviewRequest> OnPreviewWanted { get; set; }


    private string CardClass =>
        Selected ? "app-stepcard app-stepcard--selected" : "app-stepcard";

    /// <summary>
    /// The colour is data - it comes from the department row - so it has to be an
    /// inline custom property rather than a class.
    ///
    /// Set on the CARD, not on the tag, so the number badge can use it too.
    /// Every badge was the same purple whatever business unit the process
    /// belonged to, which made the colour on the tag look decorative.
    ///
    /// Falls back to the theme primary when a process has no department, rather
    /// than leaving the custom property undefined and the badge transparent.
    /// </summary>
    private string TagStyle =>
        $"--tag-colour:{Process.DepartmentColour ?? BrandPalette.DefaultDepartmentColour};";

    /// <summary>
    /// Spells out the number badge, coloured tag and status dot, none of which a
    /// screen reader can see.
    /// </summary>
    private string AriaLabel =>
        $"Process {Number}, {Process.Name}"
        + (string.IsNullOrWhiteSpace(Process.DepartmentName) ? "" : $", {Process.DepartmentName}")
        + $", {Process.StatusName}"
        + $", {Process.StepCount} {(Process.StepCount == 1 ? "step" : "steps")}";
}

/// <summary>
/// What a hover shows: the description is already on the card's DTO, but who is
/// involved has to be gathered from the steps underneath it.
/// </summary>
/// <summary>
/// Which card, and where the pointer was when it entered. The coordinates are
/// viewport coordinates, which is exactly what a position: fixed panel wants -
/// and going through the pointer avoids a JS round trip just to measure where a
/// card sits inside a rail that scrolls.
///
/// Null coordinates mean it came from keyboard focus, which has no pointer.
/// </summary>
public sealed record PreviewRequest(int ProcessId, double? X, double? Y);

public sealed record ProcessPreview(IReadOnlyList<string> People, int StepCount)
{
    public string StepLabel => StepCount == 1 ? "1 step" : $"{StepCount} steps";
}
