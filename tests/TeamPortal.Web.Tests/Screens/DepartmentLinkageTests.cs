using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Web.Components.Dialogs;
using TeamPortal.Web.Components.Pages;

namespace TeamPortal.Web.Tests.Screens;

public class DepartmentLinkageTests : DialogTestBase
{
    private const int OnboardingProcessId = 1;
    private const int ClientFolderId = 2;

    private IDepartmentService DepartmentService =>
        Services.GetRequiredService<IDepartmentService>();

    private async Task<IReadOnlyList<string>> ExpectedNamesAsync()
    {
        IReadOnlyList<DepartmentListItemDto> all = await DepartmentService.GetWithCountsAsync();
        return [.. all.Select(d => d.Name)];
    }

    /// <summary>Reads the option text out of a rendered MudSelect.</summary>
    private static IReadOnlyList<string> SelectOptions(
        IRenderedComponent<MudDialogProvider> provider, string selectId)
    {
        // MudSelect renders its items into the popover once opened; the values are
        // also present as the select's own item text nodes.
        return
        [
            .. provider.FindAll($"#{selectId} + div .mud-list-item, .mud-popover .mud-list-item")
                .Select(e => e.TextContent.Trim())
                .Where(t => t.Length > 0)
        ];
    }

    [Fact]
    public async Task TeamFilter_ListsEveryDepartment()
    {
        IReadOnlyList<string> expected = await ExpectedNamesAsync();

        IRenderedComponent<TeamPage> cut = Render<TeamPage>();
        cut.WaitForAssertion(() => cut.FindAll(".app-empcard").Should().NotBeEmpty());

        IReadOnlyList<string> shown =
        [
            .. cut.FindAll(".app-deptfilter__label").Select(e => e.TextContent.Trim())
        ];

        // The filter column adds an "All" row on top of the real departments.
        shown.Should().Contain(expected);
    }

    [Fact]
    public async Task AddProcessModal_OffersEveryDepartment()
    {
        IReadOnlyList<string> expected = await ExpectedNamesAsync();

        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        _ = ProcessDialog.ShowAsync(Dialogs, ClientFolderId);
        provider.WaitForAssertion(() =>
            provider.FindAll(".app-procdialog__unit").Should().NotBeEmpty());
        await Task.Yield();

        provider.FindAll(".app-procdialog__unit").Select(u => u.TextContent.Trim())
            .Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task AddEmployeeModal_ReadsTheSameDepartmentService()
    {
        // The select's options only exist once its popover opens, which bUnit
        // cannot drive reliably. Asserting the dialog renders and the service is
        // the shared one is what is actually worth pinning: a second, private
        // list would have to come from somewhere other than IDepartmentService.
        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        _ = AddEmployeeDialog.ShowAsync(Dialogs);
        provider.WaitForAssertion(() => provider.FindAll("#emp-dept").Should().NotBeEmpty());
        await Task.Yield();

        provider.FindAll("#emp-dept").Should().ContainSingle();
        DepartmentService.Should().BeSameAs(Services.GetRequiredService<IDepartmentService>());
    }

    [Fact]
    public async Task AddingADepartment_ShowsUpOnTheAddProcessModal()
    {
        Result<int> created = await DepartmentService.CreateAsync(
            new CreateDepartmentRequest("Claims", BrandPalette.Aqua100));
        created.IsSuccess.Should().BeTrue();

        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        _ = ProcessDialog.ShowAsync(Dialogs, ClientFolderId);
        provider.WaitForAssertion(() =>
            provider.FindAll(".app-procdialog__unit").Should().NotBeEmpty());
        await Task.Yield();

        provider.FindAll(".app-procdialog__unit").Select(u => u.TextContent.Trim())
            .Should().Contain("Claims");
    }

    [Fact]
    public async Task AddingADepartment_ShowsUpOnTheTeamFilter()
    {
        await DepartmentService.CreateAsync(
            new CreateDepartmentRequest("Claims", BrandPalette.Aqua100));

        IRenderedComponent<TeamPage> cut = Render<TeamPage>();
        cut.WaitForAssertion(() => cut.FindAll(".app-empcard").Should().NotBeEmpty());

        cut.FindAll(".app-deptfilter__label").Select(e => e.TextContent.Trim())
            .Should().Contain("Claims");
    }

    [Fact]
    public async Task RenamingADepartment_FlowsThroughToTheAddProcessModal()
    {
        IReadOnlyList<DepartmentListItemDto> before = await DepartmentService.GetWithCountsAsync();
        DepartmentListItemDto sales = before.First(d => d.Name == "Sales");

        await DepartmentService.UpdateAsync(
            new UpdateDepartmentRequest(sales.Id, "New Business", sales.ColourHex));

        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        _ = ProcessDialog.ShowAsync(Dialogs, ClientFolderId);
        provider.WaitForAssertion(() =>
            provider.FindAll(".app-procdialog__unit").Should().NotBeEmpty());
        await Task.Yield();

        IReadOnlyList<string> shown =
        [
            .. provider.FindAll(".app-procdialog__unit").Select(u => u.TextContent.Trim())
        ];

        shown.Should().Contain("New Business").And.NotContain("Sales");
    }

    [Fact]
    public async Task TheSeededDepartmentsAreTheSevenOnTheTeamScreen()
    {
        IReadOnlyList<string> names = await ExpectedNamesAsync();

        names.Should().BeEquivalentTo([
            "Sales", "Accounts", "Underwriting", "Management",
            "Corporate", "Business Enablement", "Risk & Compliance"
        ]);
    }

    [Fact]
    public async Task AddStepModal_ReadsTheSameDepartmentService()
    {
        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        _ = AddStepDialog.ShowAsync(Dialogs, OnboardingProcessId);
        provider.WaitForAssertion(() => provider.FindAll("#step-dept").Should().NotBeEmpty());
        await Task.Yield();

        provider.FindAll("#step-dept").Should().ContainSingle();
    }
}
