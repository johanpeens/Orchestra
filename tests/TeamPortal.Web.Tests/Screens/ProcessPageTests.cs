using AngleSharp.Dom;
using Bunit;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Web.Components.Pages;
using TeamPortal.Web.Components.Shared;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public class ProcessPageTests : ComponentTestBase
{
    /// <summary>
    /// Renders the page with the flow strip OPEN. The strip starts collapsed -
    /// it is the tallest thing on the page and is in the way once you are reading
    /// a process - so every test about the strip has to open it first.
    /// </summary>
    private IRenderedComponent<ProcessPage> RenderProcess()
    {
        IRenderedComponent<ProcessPage> cut = RenderCollapsed();
        cut.Find(".app-process__flowtoggle").Click();
        cut.WaitForAssertion(() => cut.FindAll(".app-stepcard").Should().NotBeEmpty());
        return cut;
    }

    /// <summary>Renders the page as it first appears: strip collapsed.</summary>
    private IRenderedComponent<ProcessPage> RenderCollapsed()
    {
        IRenderedComponent<ProcessPage> cut = Render<ProcessPage>();
        cut.WaitForAssertion(() => cut.FindAll(".app-process__flowtoggle").Should().NotBeEmpty());
        return cut;
    }

    private static IReadOnlyList<IElement> Cards(IRenderedComponent<ProcessPage> cut) =>
        cut.FindAll(".app-stepcard");

    private static IElement BackButton(IRenderedComponent<ProcessPage> cut) =>
        cut.FindAll(".app-process__nav button")[0];

    private static IElement NextButton(IRenderedComponent<ProcessPage> cut) =>
        cut.FindAll(".app-process__nav button")[^1];

    // --- Flow strip shows PROCESSES ------------------------------------------

    [Fact]
    public void ProcessPage_TheStripShowsTheSevenSeededProcesses()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        Cards(cut).Should().HaveCount(7);
        cut.Markup.Should().Contain("Initial Contact").And.Contain("Handover &amp; Kick-off");
    }

    [Fact]
    public void ProcessPage_NumbersTheStripFromOne()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.FindAll(".app-stepcard__number").Select(e => e.TextContent.Trim())
            .Should().Equal("1", "2", "3", "4", "5", "6", "7");
    }

    [Fact]
    public void ProcessPage_ShowsEachProcessCardsUnitAsTheBadgeColour()
    {
        // The chip under the name is gone - the number badge is painted the
        // unit's colour and the legend in the header says which is which.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-stepcard").GetAttribute("style").Should().Contain("--tag-colour:#");
    }

    [Fact]
    public void ProcessPage_OnFirstRender_SelectsTheFirstProcess()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-stepcard--selected").TextContent.Should().Contain("Initial Contact");
    }

    [Fact]
    public void ProcessPage_ShowsTheSelectedProcessDescription()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-page-header__description").TextContent
            .Should().Contain("Receive and qualify new client inquiries.");
    }

    [Fact]
    public void ProcessPage_TheAddButtonJustSaysAdd()
    {
        // It used to say "Add Process" while a separate plus in the rail added a
        // step - two buttons for one intention. One button asks which.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-process__add").TextContent.Trim().Should().Be("Add");
    }

    [Fact]
    public void ProcessPage_HasOnlyOneAddControl()
    {
        // The floating plus at the foot of the step rail is gone with it.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.FindAll(".app-process__add").Should().ContainSingle();
        cut.FindAll(".app-process__addfab").Should().BeEmpty();
    }

    // --- Step list shows the selected process's STEPS -------------------------

    [Fact]
    public void ProcessPage_TheStepListShowsTheSelectedProcessesSteps()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-process__listname").TextContent.Should().Contain("Initial Contact");
        cut.FindAll(".app-process__row").Should().HaveCount(4);
        cut.Markup.Should().Contain("Receive the inquiry");
    }

    [Fact]
    public void ProcessPage_ShowsTheSelectedStepsDescriptionAndOwners()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-process__sidebody").TextContent
            .Should().Contain("The initial contact step involves receiving");
        cut.FindAll(".app-process__owner").Should().HaveCount(4);
    }

    [Fact]
    public async Task ProcessPage_SelectingAProcessWithNoSteps_ShowsTheEmptyState()
    {
        // Only "Initial Contact" has steps written up in the seed.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        await Cards(cut)[1].ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".app-process__row").Should().BeEmpty();
            cut.Markup.Should().Contain("No steps written up yet.");
        });
    }

    [Fact]
    public async Task ProcessPage_SelectingAProcess_UpdatesTheStepList()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        await Cards(cut)[2].ClickAsync(new());

        cut.WaitForAssertion(() =>
            cut.Find(".app-process__listname").TextContent.Should().Contain("Review & Assess"));
    }

    [Fact]
    public async Task ProcessPage_SelectingAStep_ShowsThatStepsDetail()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        await cut.FindAll(".app-process__row")[2].ClickAsync(new());

        cut.Find(".app-process__detailhead").TextContent
            .Should().Contain("Step 3: Create the lead record");
    }

    // --- Back / Next walk steps AND cross processes ----------------------------

    [Fact]
    public void ProcessPage_OnTheVeryFirstStep_BackIsDisabled()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        BackButton(cut).HasAttribute("disabled").Should().BeTrue();
        NextButton(cut).HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPage_Next_WalksThroughTheStepsOfTheCurrentProcess()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        await NextButton(cut).ClickAsync(new());

        cut.Find(".app-process__detailhead").TextContent
            .Should().Contain("Step 2: Verify basic information");
    }

    [Fact]
    public async Task ProcessPage_NextPastTheLastStep_MovesToTheNextProcess()
    {
        // The behaviour the whole hierarchy exists for: run off the end of one
        // process's steps and you land in the next process.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        for (int i = 0; i < 4; i++)
        {
            await NextButton(cut).ClickAsync(new());
        }

        cut.WaitForAssertion(() =>
            cut.Find(".app-stepcard--selected").TextContent.Should().Contain("Gather Info"));
    }

    [Fact]
    public async Task ProcessPage_BackFromTheStartOfAProcess_ReturnsToThePreviousOne()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        // Into the second process...
        for (int i = 0; i < 4; i++)
        {
            await NextButton(cut).ClickAsync(new());
        }

        cut.WaitForAssertion(() =>
            cut.Find(".app-stepcard--selected").TextContent.Should().Contain("Gather Info"));

        // ...and straight back out again.
        await BackButton(cut).ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            cut.Find(".app-stepcard--selected").TextContent.Should().Contain("Initial Contact");
            cut.Find(".app-process__detailhead").TextContent
                .Should().Contain("Step 4: Acknowledge the client",
                    "Back must land on the LAST step of the previous process");
        });
    }

    [Fact]
    public async Task ProcessPage_OnTheVeryLastProcess_NextIsDisabled()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        await Cards(cut)[6].ClickAsync(new());

        cut.WaitForAssertion(() =>
            NextButton(cut).HasAttribute("disabled").Should().BeTrue());
    }

    [Fact]
    public void ProcessPage_ShowsWhereYouAreInTheFolder()
    {
        // Next crosses process boundaries silently, so the position has to be
        // written down or the user loses track.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-process__navhint").TextContent
            .Should().Contain("Process 1 of 7").And.Contain("Step 1 of 4");
    }

    [Fact]
    public void ProcessPage_MarksTheSelectedProcessForScreenReaders()
    {
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-stepcard--selected").GetAttribute("aria-current").Should().Be("step");
    }
    // --- The strip starts closed, and the legend stands in for it ------------

    [Fact]
    public void ProcessPage_StartsWithTheFlowStripClosed()
    {
        // It is seven cards tall and you have already chosen your process by the
        // time you are reading the steps.
        IRenderedComponent<ProcessPage> cut = RenderCollapsed();

        cut.FindAll(".app-flowstrip").Should().BeEmpty();
        cut.Find(".app-process__flowtoggle").GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public void ProcessPage_WhileTheStripIsOpen_ShowsALegendOfTheBusinessUnits()
    {
        // The legend explains the strip, so it appears WITH the strip. Each
        // card's number badge is painted its unit's colour and nothing else on
        // screen says which colour is which.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.FindAll(".app-flowlegend__item").Should().NotBeEmpty();
        cut.Markup.Should().Contain("Sales");
    }

    [Fact]
    public void ProcessPage_WhileTheStripIsClosed_ShowsNoLegend()
    {
        // Nothing to explain: there are no cards on screen to key.
        IRenderedComponent<ProcessPage> cut = RenderCollapsed();

        cut.FindAll(".app-flowlegend").Should().BeEmpty();
    }

    [Fact]
    public void ProcessPage_TheLegend_SitsInTheHeaderNotOverTheStrip()
    {
        // In the page header's action row, ahead of the icon buttons - not a
        // band of its own between the header and the strip.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.Find(".app-page-header__actions .app-flowlegend").Should().NotBeNull();
    }

    [Fact]
    public void ProcessPage_TheLegend_CarriesEachUnitsOwnColour()
    {
        // Not the theme primary on every chip - that would make the colours
        // decorative rather than the ones set on the Team screen.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        IReadOnlyList<string> styles =
        [
            .. cut.FindAll(".app-flowlegend__item")
                .Select(e => e.GetAttribute("style") ?? string.Empty)
        ];

        styles.Should().AllSatisfy(s => s.Should().Contain("--tag-colour:#"));
    }

    [Fact]
    public void ProcessPage_TheLegend_NamesEachBusinessUnitOnce()
    {
        // Five of the seven processes are Sales or Management. A legend is a list
        // of units, not a list of processes.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.FindAll(".app-flowlegend__name")
            .Select(e => e.TextContent.Trim())
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ProcessPage_TheCards_NoLongerRepeatTheUnitAsAChip()
    {
        // Three times over was too many: the badge colour, the chip and the
        // legend all said the same thing. The chip went.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.FindAll(".app-stepcard .app-stepcard__tag").Should().BeEmpty();
    }

    [Fact]
    public void ProcessPage_TheStepDetail_StillNamesItsUnit()
    {
        // Only the strip's cards lost their chip. The step you are reading still
        // says which unit it belongs to, next to its title.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.FindAll(".app-process__detailhead .app-stepcard__tag").Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessPage_TheCards_StillNameTheirUnitToAScreenReader()
    {
        // Losing the chip must not lose the information for anyone who cannot
        // see the badge's colour.
        IRenderedComponent<ProcessPage> cut = RenderProcess();

        cut.FindAll(".app-stepcard")
            .Select(e => e.GetAttribute("aria-label") ?? string.Empty)
            .Should().Contain(label => label.Contains("Sales", StringComparison.Ordinal));
    }
}

public class ProcessFlowCardTests : ComponentTestBase
{
    private static ProcessListItemDto Card(
        string name = "Initial Contact",
        string? department = "Sales",
        string status = "In Progress",
        int stepCount = 4) =>
        new(1, name, "A description", 1, 1, department, "#E55218", 2, status, "#44B4A6", stepCount);

    [Fact]
    public void ProcessFlowCard_SpellsOutEverythingInItsAriaLabel()
    {
        // The number badge, the coloured tag, the status dot and the step count
        // are all visual. Without this the card announces only its name.
        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(x => x.Process, Card())
            .Add(x => x.Number, 1));

        cut.Find("button").GetAttribute("aria-label")
            .Should().Be("Process 1, Initial Contact, Sales, In Progress, 4 steps");
    }

    [Fact]
    public void ProcessFlowCard_WithOneStep_UsesTheSingular()
    {
        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(x => x.Process, Card(stepCount: 1))
            .Add(x => x.Number, 1));

        cut.Find("button").GetAttribute("aria-label").Should().EndWith("1 step");
    }

    [Fact]
    public void ProcessFlowCard_WithNoDepartmentColour_FallsBackToTheDefault()
    {
        // The badge still has to be painted something, or the number goes
        // white-on-white.
        ProcessListItemDto noColour = new(
            1, "Initial Contact", "A description", 1, null, null, null,
            2, "In Progress", "#44B4A6", 4);

        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(x => x.Process, noColour)
            .Add(x => x.Number, 1));

        cut.Find(".app-stepcard").GetAttribute("style")
            .Should().Contain(BrandPalette.DefaultDepartmentColour);
    }

    [Fact]
    public void ProcessFlowCard_CarriesTheDepartmentColourOnTheCard()
    {
        // On the CARD, not on the tag. Both the tag and the number badge use it,
        // and the badge used to be the theme purple on every card whatever
        // business unit the process belonged to - which made the coloured tag
        // underneath look decorative rather than meaningful.
        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(x => x.Process, Card())
            .Add(x => x.Number, 1));

        cut.Find(".app-stepcard").GetAttribute("style").Should().Contain("#E55218");
    }

    [Fact]
    public void ProcessFlowCard_WithNoDepartment_StillHasAColour()
    {
        // An undefined custom property leaves the badge transparent.
        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(x => x.Process, Card(department: null))
            .Add(x => x.Number, 1));

        cut.Find(".app-stepcard").GetAttribute("style").Should().Contain("--tag-colour:#");
    }

    [Fact]
    public void ProcessFlowCard_UsesTheStatusColourForItsDot()
    {
        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(x => x.Process, Card())
            .Add(x => x.Number, 1));

        cut.Find(".app-stepcard__dot").GetAttribute("style").Should().Contain("#44B4A6");
    }

    [Fact]
    public async Task ProcessFlowCard_OnClick_RaisesOnSelectWithTheProcessId()
    {
        int? selected = null;

        IRenderedComponent<ProcessFlowCard> cut = Render<ProcessFlowCard>(p => p
            .Add(x => x.Process, Card())
            .Add(x => x.Number, 1)
            .Add(x => x.OnSelect,
                Microsoft.AspNetCore.Components.EventCallback.Factory
                    .Create<int>(this, id => selected = id)));

        await cut.Find("button").ClickAsync(new());

        selected.Should().Be(1);
    }
}
