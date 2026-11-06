using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Web.Components.Dialogs;
using TeamPortal.Web.Components.Shared;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public class ProcessFlowCardHoverTests : ComponentTestBase
{
    private static ProcessListItemDto Card(string? description = "Receive and qualify inquiries.") =>
        new(
            Id: 1,
            Name: "Initial Contact",
            Description: description,
            SortOrder: 1,
            DepartmentId: 1,
            DepartmentName: "Sales",
            DepartmentColour: "#CF1230",
            StepStatusId: 2,
            StatusName: "In Progress",
            StatusColour: "#E55218",
            StepCount: 4);

    [Fact(Skip = "Hover preview is switched off - see the note atop ProcessFlowCard.razor.")]
    public void TheCard_AsksForItsPreviewOnPointerEnter()
    {
        // MouseEnter, not MouseOver: mouseover bubbles and re-fires on every child,
        // which flooded the circuit and hung the page. Lazily too, so opening a
        // folder does not fetch seven previews nobody has asked for.
        int? asked = null;

        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(c => c.Process, Card())
            .Add(c => c.Number, 1)
            .Add(c => c.OnPreviewWanted, (PreviewRequest r) => asked = r.ProcessId));

        cut.Find(".app-stepcard").MouseEnter();

        asked.Should().Be(1);
    }

    [Fact(Skip = "Hover preview is switched off - see the note atop ProcessFlowCard.razor.")]
    public void TheCard_AsksForItsPreviewOnKeyboardFocus()
    {
        // A hover-only preview is invisible to anyone tabbing through the strip.
        int? asked = null;

        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(c => c.Process, Card())
            .Add(c => c.Number, 1)
            .Add(c => c.OnPreviewWanted, (PreviewRequest r) => asked = r.ProcessId));

        cut.Find(".app-stepcard").Focus();

        asked.Should().Be(1);
    }

    [Fact]
    public void TheCard_RaisesOpenDetailOnDoubleClick()
    {
        int? opened = null;

        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(c => c.Process, Card())
            .Add(c => c.Number, 1)
            .Add(c => c.OnOpenDetail, (int id) => opened = id));

        cut.Find(".app-stepcard").DoubleClick();

        opened.Should().Be(1);
    }

    [Fact]
    public void ASingleClick_StillJustSelects()
    {
        // Double-click must not cost the single click its meaning.
        int? selected = null;
        int? opened = null;

        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(c => c.Process, Card())
            .Add(c => c.Number, 1)
            .Add(c => c.OnSelect, (int id) => selected = id)
            .Add(c => c.OnOpenDetail, (int id) => opened = id));

        cut.Find(".app-stepcard").Click();

        selected.Should().Be(1);
        opened.Should().BeNull();
    }

    // --- What the preview says ----------------------------------------------
    //
    // Against ProcessPreviewPanel directly. The card wraps it in a MudTooltip,
    // which hands its content to the popover service - that never lands in the
    // card's own DOM, so a test there would be asserting on MudBlazor's plumbing
    // rather than on anything this app decides.

    private IRenderedComponent<ProcessPreviewPanel> Preview(
        ProcessListItemDto card, ProcessPreview? detail) =>
        Render<ProcessPreviewPanel>(p => p
            .Add(c => c.Process, card)
            .Add(c => c.Detail, detail));

    [Fact]
    public void ThePreview_ShowsTheDescriptionAndThePeople()
    {
        IRenderedComponent<ProcessPreviewPanel> cut = Preview(
            Card(),
            new ProcessPreview(["Sarah - Project Manager", "David Chen - Solutions Developer"], 4));

        cut.Markup.Should().Contain("Receive and qualify inquiries.");
        cut.Markup.Should().Contain("Sarah - Project Manager");
        cut.Markup.Should().Contain("David Chen");
    }

    [Fact]
    public void ThePreview_WithNoDescription_SaysSoRatherThanShowingAGap()
    {
        IRenderedComponent<ProcessPreviewPanel> cut =
            Preview(Card(description: null), new ProcessPreview([], 0));

        cut.Markup.Should().Contain("No description written up yet.");
        cut.Markup.Should().Contain("Nobody assigned yet");
    }

    [Fact]
    public void ThePreview_BeforeItHasLoaded_SaysSo()
    {
        IRenderedComponent<ProcessPreviewPanel> cut = Preview(Card(), detail: null);

        cut.Markup.Should().Contain("Loading");
    }

    [Fact]
    public void ThePreview_SaysHowToOpenTheFullThing()
    {
        // Double-click is undiscoverable unless something says so.
        IRenderedComponent<ProcessPreviewPanel> cut = Preview(Card(), new ProcessPreview([], 4));

        cut.Markup.Should().Contain("Double-click to open");
    }

    [Fact]
    public void ThePreview_ShowsTheStatusAndTheDepartment()
    {
        IRenderedComponent<ProcessPreviewPanel> cut = Preview(Card(), new ProcessPreview([], 4));

        cut.Markup.Should().Contain("In Progress");
        cut.Markup.Should().Contain("Sales");
    }

    [Theory]
    [InlineData(0, "0 steps")]
    [InlineData(1, "1 step")]
    [InlineData(4, "4 steps")]
    public void ThePreview_PluralisesTheStepCount(int count, string expected)
    {
        new ProcessPreview([], count).StepLabel.Should().Be(expected);
    }
}

public class ProcessDetailDialogTests : DialogTestBase
{
    private async Task<(IRenderedComponent<MudDialogProvider> Provider, Task Shown)>
        OpenAsync(int? processId = null)
    {
        int id = processId ?? await OnboardingProcessIdAsync();

        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        Task shown = ProcessDetailDialog.ShowAsync(Dialogs, id);

        provider.WaitForAssertion(
            () => provider.Markup.Should().NotContain("Loading the process"));

        return (provider, shown);
    }

    [Fact]
    public async Task TheDialog_ShowsTheDescriptionEveryStepAndEveryone()
    {
        var (provider, _) = await OpenAsync();

        provider.Markup.Should().Contain("Initial Contact");
        provider.Markup.Should().Contain("Receive the inquiry");
        provider.Markup.Should().Contain("Sarah");
        provider.Markup.Should().Contain("Who is involved");
    }

    [Fact]
    public async Task TheDialog_ListsEachPersonOnce()
    {
        // Sarah owns more than one step. "Who is involved" is a list of people,
        // not a tally of assignments.
        var (provider, _) = await OpenAsync();

        IElement people = provider.Find(".app-detaildialog__people");

        people.QuerySelectorAll(".app-personoption__name")
            .Select(e => e.TextContent.Trim())
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task TheDialog_ShowsEveryStepInOrder()
    {
        var (provider, _) = await OpenAsync();

        IReadOnlyList<string> numbers =
        [
            .. provider.FindAll(".app-detaildialog__stepnum").Select(e => e.TextContent.Trim())
        ];

        numbers.Should().Equal(["1", "2", "3", "4"]);
    }

    [Fact]
    public async Task TheDialog_ForAProcessThatIsGone_SaysSo()
    {
        var (provider, _) = await OpenAsync(999_999);

        provider.Markup.Should().Contain("no longer there");
    }

    [Fact]
    public async Task TheDialog_ChangesNothing()
    {
        // Read-only on purpose: a stray double-click must not be able to open
        // something that can edit. The only action is Close.
        var (provider, _) = await OpenAsync();

        IReadOnlyList<string> actions =
        [
            .. provider.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim())
        ];

        actions.Should().ContainSingle().Which.Should().Be("Close");
    }

    private async Task<int> OnboardingProcessIdAsync()
    {
        IProcessFolderService folders = Services.GetRequiredService<IProcessFolderService>();
        IProcessService processes = Services.GetRequiredService<IProcessService>();

        IReadOnlyList<ProcessFolderDto> all = await folders.GetWithCountsAsync();
        int folderId = all.First(f => f.Name == "Client Onboarding").Id;

        IReadOnlyList<ProcessListItemDto> inFolder = await processes.GetByFolderAsync(folderId);

        return inFolder.First(p => p.Name == "Initial Contact").Id;
    }
}
