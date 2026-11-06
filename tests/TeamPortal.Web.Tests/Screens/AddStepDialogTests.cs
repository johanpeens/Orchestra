using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Web.Components.Dialogs;

namespace TeamPortal.Web.Tests.Screens;

public class AddStepDialogTests : DialogTestBase
{
    // Process 1 is "Initial Contact" - the only seeded process with steps.
    private const int OnboardingProcessId = 1;
    private const int FirstStepId = 1;

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, Task<bool> Result)>
        OpenAsync(int? stepId = null)
    {
        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        Task<bool> result = AddStepDialog.ShowAsync(Dialogs, OnboardingProcessId, stepId);
        provider.WaitForAssertion(() => provider.FindAll("#step-name").Should().NotBeEmpty());
        await Task.Yield();
        return (provider, result);
    }

    private static IElement SaveStepButton(IRenderedComponent<MudDialogProvider> p) =>
        p.FindAll(".mud-dialog-actions button")
            .First(b => b.TextContent.Contains("Save", StringComparison.Ordinal));

    private static IElement CancelStepButton(IRenderedComponent<MudDialogProvider> p) =>
        p.FindAll(".mud-dialog-actions button")
            .First(b => b.TextContent.Contains("Cancel", StringComparison.Ordinal));

    private IProcessStepService StepService => Services.GetRequiredService<IProcessStepService>();

    // --- Add mode ----------------------------------------------------------------

    [Fact]
    public async Task AddStepDialog_InAddMode_StartsEmptyWithSaveDisabled()
    {
        var (provider, _) = await OpenAsync();

        provider.Find("#step-name").GetAttribute("value").Should().BeNullOrEmpty();
        SaveStepButton(provider).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task AddStepDialog_InAddMode_HasNoOwnersAndNoDeleteButton()
    {
        var (provider, _) = await OpenAsync();

        provider.Markup.Should().Contain("Nobody assigned yet.");
        provider.Markup.Should().NotContain("Delete Step");
    }

    [Fact]
    public async Task AddStepDialog_ShowsTheUploadRulesFromTheDesign()
    {
        var (provider, _) = await OpenAsync();

        provider.Find(".app-stepdialog__drop").TextContent
            .Should().Contain("PNG, JPG or WEBP up to 5MB");
    }

    [Fact]
    public async Task AddStepDialog_OnSave_AppendsTheStepWithTheNextNumber()
    {
        var (provider, result) = await OpenAsync();

        await provider.Find("#step-name").InputAsync(new() { Value = "Final Review" });
        await SaveStepButton(provider).ClickAsync(new());
        (await result).Should().BeTrue();

        IReadOnlyList<ProcessStepDto> steps = await StepService.GetByProcessAsync(OnboardingProcessId);
        steps.Should().HaveCount(5);
        steps[^1].Name.Should().Be("Final Review");
        steps[^1].StepNumber.Should().Be(5);
    }

    [Fact]
    public async Task AddStepDialog_WithATooShortName_StaysOpen()
    {
        var (provider, result) = await OpenAsync();

        await provider.Find("#step-name").InputAsync(new() { Value = "ab" });
        await SaveStepButton(provider).ClickAsync(new());

        result.IsCompleted.Should().BeFalse();
        provider.Markup.Should().Contain("at least 3 characters");
    }

    [Fact]
    public async Task AddStepDialog_OnCancel_CreatesNothing()
    {
        var (provider, result) = await OpenAsync();

        await provider.Find("#step-name").InputAsync(new() { Value = "Discarded" });
        await CancelStepButton(provider).ClickAsync(new());

        (await result).Should().BeFalse();
        (await StepService.GetByProcessAsync(OnboardingProcessId)).Should().HaveCount(4);
    }

    [Fact]
    public async Task AddStepDialog_WhenTheSaveFails_StaysOpenAndShowsTheError()
    {
        var (provider, result) = await OpenAsync();
        await provider.Find("#step-name").InputAsync(new() { Value = "Final Review" });
        Services.GetRequiredService<TeamPortal.Mock.MockOptions>().FailNextWrite = true;

        await SaveStepButton(provider).ClickAsync(new());

        result.IsCompleted.Should().BeFalse();
        provider.FindAll("[role=alert]").Should().NotBeEmpty();
    }

    // --- Owner chips ---------------------------------------------------------------

    [Fact]
    public async Task AddStepDialog_OnAddTeamMember_ShowsThePicker()
    {
        var (provider, _) = await OpenAsync();

        await provider.FindAll("button")
            .First(b => b.TextContent.Contains("Add Team Member", StringComparison.Ordinal))
            .ClickAsync(new());

        provider.FindAll(".app-stepdialog__picker").Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddStepDialog_InEditMode_ShowsAChipPerExistingOwner()
    {
        var (provider, _) = await OpenAsync(FirstStepId);

        provider.FindAll(".app-stepdialog__chip").Should().HaveCount(4);
        provider.Markup.Should().Contain("Sarah").And.Contain("David Chen");
    }

    [Fact]
    public async Task AddStepDialog_EveryRemoveChipButtonIsLabelled()
    {
        var (provider, _) = await OpenAsync(FirstStepId);

        provider.FindAll(".app-stepdialog__chipremove").Should()
            .OnlyContain(b => b.GetAttribute("aria-label")!.StartsWith("Remove ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddStepDialog_OnRemovingAChip_DropsThatOwner()
    {
        var (provider, _) = await OpenAsync(FirstStepId);

        await provider.FindAll(".app-stepdialog__chipremove")[0].ClickAsync(new());

        provider.FindAll(".app-stepdialog__chip").Should().HaveCount(3);
    }

    [Fact]
    public async Task AddStepDialog_OnSavingAfterRemovingAnOwner_PersistsTheChange()
    {
        var (provider, result) = await OpenAsync(FirstStepId);

        await provider.FindAll(".app-stepdialog__chipremove")[0].ClickAsync(new());
        await SaveStepButton(provider).ClickAsync(new());
        (await result).Should().BeTrue();

        ProcessStepDto? step = await StepService.GetByIdAsync(FirstStepId);
        step!.Owners.Should().HaveCount(3);
    }

    // --- Edit mode -------------------------------------------------------------------

    [Fact]
    public async Task AddStepDialog_InEditMode_PrefillsTheCurrentValues()
    {
        var (provider, _) = await OpenAsync(FirstStepId);

        provider.Find("#step-name").GetAttribute("value").Should().Be("Receive the inquiry");
        provider.Find("#step-desc").TextContent
            .Should().Contain("The initial contact step involves receiving");
    }

    [Fact]
    public async Task AddStepDialog_InEditMode_ShowsTheDeleteButton()
    {
        var (provider, _) = await OpenAsync(FirstStepId);

        provider.Markup.Should().Contain("Delete Step");
    }

    [Fact]
    public async Task AddStepDialog_InEditMode_RenamingPersists()
    {
        var (provider, result) = await OpenAsync(FirstStepId);

        await provider.Find("#step-name").InputAsync(new() { Value = "First Contact" });
        await SaveStepButton(provider).ClickAsync(new());
        (await result).Should().BeTrue();

        ProcessStepDto? step = await StepService.GetByIdAsync(FirstStepId);
        step!.Name.Should().Be("First Contact");
    }

    [Fact]
    public async Task AddStepDialog_InEditMode_KeepsTheStepNumber()
    {
        var (provider, result) = await OpenAsync(FirstStepId);

        await provider.Find("#step-name").InputAsync(new() { Value = "First Contact" });
        await SaveStepButton(provider).ClickAsync(new());
        (await result).Should().BeTrue();

        ProcessStepDto? step = await StepService.GetByIdAsync(FirstStepId);
        step!.StepNumber.Should().Be(1, "editing a step must not reorder the strip");
    }

    // NOTE: as with ProcessDialog, the delete-confirm path is not driven here -
    // bUnit deadlocks on a dialog opened from inside a dialog it is awaiting.
    // ConfirmDialogTests and the step service's renumbering contract tests cover
    // the two halves.

    // --- Three tabs, one form -------------------------------------------------

    [Fact]
    public async Task AddStepDialog_SplitsTheFormIntoStepPeopleAndScreenshot()
    {
        // One long scroll pushed Save below the fold and left the drop zone as
        // the last thing you saw.
        var (provider, _) = await OpenAsync();

        IReadOnlyList<string> tabs =
        [
            .. provider.FindAll(".mud-tabs-tabbar .mud-tab")
                .Select(t => t.TextContent.Trim())
        ];

        tabs.Should().HaveCount(3);
        tabs[0].Should().Contain("Step");
        tabs[1].Should().Contain("People");
        tabs[2].Should().Contain("Screenshot");
    }

    [Fact]
    public async Task AddStepDialog_KeepsSaveAndCancelOutsideTheTabs()
    {
        // Saving has to be reachable from whichever tab you happen to be on.
        var (provider, _) = await OpenAsync();

        provider.FindAll(".mud-dialog-actions .mud-tab").Should().BeEmpty();
        SaveStepButton(provider).Should().NotBeNull();
        CancelStepButton(provider).Should().NotBeNull();
    }

    [Fact]
    public async Task AddStepDialog_KeepsEveryPanelInTheDom()
    {
        // The three panels are one form. MudTabs discards the panel you leave
        // unless told otherwise, which would tear down a half-filled upload.
        var (provider, _) = await OpenAsync();

        provider.FindAll("#step-name").Should().NotBeEmpty();
        provider.Markup.Should().Contain("Responsible Persons");
        provider.Markup.Should().Contain("Upload Reference Screenshot");
    }

    [Fact]
    public async Task AddStepDialog_CountsWhatIsOnTheHiddenTabs()
    {
        // So you can see there is a person and a screenshot attached without
        // opening each tab to find out.
        var (provider, _) = await OpenAsync(FirstStepId);

        IReadOnlyList<string> tabs =
        [
            .. provider.FindAll(".mud-tabs-tabbar .mud-tab")
                .Select(t => t.TextContent.Trim())
        ];

        tabs[1].Should().Contain("(");
    }
}
