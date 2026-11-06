using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Web.Components.Dialogs;

namespace TeamPortal.Web.Tests.Screens;

public class ProcessDialogTests : DialogTestBase
{
    // Folder 2 is "Client Onboarding"; process 1 is "Initial Contact".
    private const int ClientFolderId = 2;
    private const int OnboardingProcessId = 1;

    private async Task<(IRenderedComponent<MudDialogProvider> Provider, Task<bool> Result)>
        OpenAsync(int? processId = null)
    {
        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        Task<bool> result = ProcessDialog.ShowAsync(Dialogs, ClientFolderId, processId);
        provider.WaitForAssertion(() => provider.FindAll("#proc-name").Should().NotBeEmpty());
        await Task.Yield();
        return (provider, result);
    }

    private static IElement SaveProcessButton(IRenderedComponent<MudDialogProvider> p) =>
        p.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Save", StringComparison.Ordinal));

    private static IElement CancelProcessButton(IRenderedComponent<MudDialogProvider> p) =>
        p.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Cancel", StringComparison.Ordinal));

    // --- Add mode ---------------------------------------------------------------

    [Fact]
    public async Task ProcessDialog_InAddMode_StartsEmptyWithSaveDisabled()
    {
        var (provider, _) = await OpenAsync();

        provider.Find("#proc-name").GetAttribute("value").Should().BeNullOrEmpty();
        SaveProcessButton(provider).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ProcessDialog_InAddMode_HasNoDeleteButton()
    {
        var (provider, _) = await OpenAsync();

        provider.Markup.Should().NotContain("Delete Process");
    }

    [Fact]
    public async Task ProcessDialog_OffersTheSameUnitsTheTeamScreenLists()
    {
        // One source, two screens. A unit added on the Team screen has to turn up
        // here without anyone wiring it up a second time.
        var (provider, _) = await OpenAsync();

        IReadOnlyList<DepartmentListItemDto> onTeamScreen =
            await Services.GetRequiredService<IDepartmentService>().GetWithCountsAsync();

        provider.FindAll(".app-procdialog__unit").Select(u => u.TextContent.Trim())
            .Should().BeEquivalentTo(onTeamScreen.Select(d => d.Name));
    }

    [Fact]
    public async Task ProcessDialog_ANewlyAddedUnit_AppearsInTheSelector()
    {
        await Services.GetRequiredService<IDepartmentService>()
            .CreateAsync(new CreateDepartmentRequest("Claims", BrandPalette.Aqua100));

        var (provider, _) = await OpenAsync();

        provider.Markup.Should().Contain("Claims");
    }

    [Fact]
    public async Task ProcessDialog_OnPickingAUnit_MarksItChecked()
    {
        var (provider, _) = await OpenAsync();

        await provider.FindAll(".app-procdialog__unit")[1].ClickAsync(new());

        provider.FindAll(".app-procdialog__unit")[1]
            .GetAttribute("aria-checked").Should().Be("true");
    }

    [Fact]
    public async Task ProcessDialog_ClickingTheSelectedUnitAgain_ClearsIt()
    {
        // Business unit is optional, so there has to be a way back to "none".
        var (provider, _) = await OpenAsync();
        await provider.FindAll(".app-procdialog__unit")[1].ClickAsync(new());

        await provider.FindAll(".app-procdialog__unit")[1].ClickAsync(new());

        provider.FindAll(".app-procdialog__unit")
            .Should().OnlyContain(u => u.GetAttribute("aria-checked") == "false");
    }

    [Fact]
    public async Task ProcessDialog_OnSave_CreatesTheProcess()
    {
        var (provider, result) = await OpenAsync();
        IProcessService processes = Services.GetRequiredService<IProcessService>();
        int before = (await processes.GetByFolderAsync(ClientFolderId)).Count;

        await provider.Find("#proc-name").InputAsync(new() { Value = "Renewals Process" });
        await SaveProcessButton(provider).ClickAsync(new());

        (await result).Should().BeTrue();
        (await processes.GetByFolderAsync(ClientFolderId)).Should().HaveCount(before + 1);
    }

    [Fact]
    public async Task ProcessDialog_WithADuplicateNameInTheFolder_StaysOpen()
    {
        var (provider, result) = await OpenAsync();

        await provider.Find("#proc-name").InputAsync(new() { Value = "Initial Contact" });
        await SaveProcessButton(provider).ClickAsync(new());

        provider.Markup.Should().Contain("already exists in this folder");
        result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessDialog_WithATooShortName_StaysOpen()
    {
        var (provider, result) = await OpenAsync();

        await provider.Find("#proc-name").InputAsync(new() { Value = "ab" });
        await SaveProcessButton(provider).ClickAsync(new());

        result.IsCompleted.Should().BeFalse();
        provider.Markup.Should().Contain("at least 3 characters");
    }

    [Fact]
    public async Task ProcessDialog_OnCancel_CreatesNothing()
    {
        var (provider, result) = await OpenAsync();
        IProcessService processes = Services.GetRequiredService<IProcessService>();
        int before = (await processes.GetByFolderAsync(ClientFolderId)).Count;

        await provider.Find("#proc-name").InputAsync(new() { Value = "Discarded" });
        await CancelProcessButton(provider).ClickAsync(new());

        (await result).Should().BeFalse();
        (await processes.GetByFolderAsync(ClientFolderId)).Should().HaveCount(before);
    }

    // --- Edit mode --------------------------------------------------------------

    [Fact]
    public async Task ProcessDialog_InEditMode_PrefillsTheCurrentValues()
    {
        var (provider, _) = await OpenAsync(OnboardingProcessId);

        provider.Find("#proc-name").GetAttribute("value").Should().Be("Initial Contact");
        provider.Find("#proc-desc").TextContent
            .Should().Contain("Receive and qualify new client inquiries.");
    }

    [Fact]
    public async Task ProcessDialog_InEditMode_ShowsTheDeleteButton()
    {
        var (provider, _) = await OpenAsync(OnboardingProcessId);

        provider.Markup.Should().Contain("Delete Process");
    }

    [Fact]
    public async Task ProcessDialog_InEditMode_SavingWithoutRenaming_Succeeds()
    {
        var (provider, result) = await OpenAsync(OnboardingProcessId);

        await SaveProcessButton(provider).ClickAsync(new());

        (await result).Should().BeTrue();
    }

    // NOTE: the "delete asks for confirmation" path is deliberately NOT driven
    // here. ConfirmDialog is opened from inside ProcessDialog's own event
    // handler, and bUnit deadlocks awaiting a dialog raised from a dialog it is
    // already awaiting. The pieces are covered separately instead:
    //   - ConfirmDialogTests proves the confirm returns true only on confirm.
    //   - MockProcessServiceTests.DeleteAsync_AlsoRemovesItsSteps proves the
    //     delete itself.
    // The wiring between them is checked by hand until phase A12's demo.
}
