using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Web.Components.Dialogs;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

/// <summary>
/// A MudDialog needs a MudDialogProvider in the render tree, so each test renders
/// the provider and then opens the dialog through IDialogService - the same path
/// the real pages use.
/// </summary>
public abstract class DialogTestBase : ComponentTestBase
{
    protected IRenderedComponent<MudDialogProvider> OpenProvider()
    {
        IRenderedComponent<MudDialogProvider> provider = Render<MudDialogProvider>();
        return provider;
    }

    protected IDialogService Dialogs => Services.GetRequiredService<IDialogService>();

    protected static IElement SaveButton(IRenderedComponent<MudDialogProvider> p) =>
        p.FindAll(".mud-dialog-actions button")[0];

    protected static IElement CancelButton(IRenderedComponent<MudDialogProvider> p) =>
        p.FindAll(".mud-dialog-actions button")[^1];
}

public class EditDepartmentDialogTests : DialogTestBase
{
    private async Task<(IRenderedComponent<MudDialogProvider> Provider,
        Task<DepartmentDialogOutcome> Result)>
        OpenAsync(int? departmentId = 6)
    {
        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        Task<DepartmentDialogOutcome> result =
            EditDepartmentDialog.ShowAsync(Dialogs, departmentId);
        provider.WaitForAssertion(() => provider.FindAll("#dept-name").Should().NotBeEmpty());
        await Task.Yield();
        return (provider, result);
    }

    [Fact]
    public async Task EditDepartmentDialog_OnOpen_PrefillsTheCurrentValues()
    {
        var (provider, _) = await OpenAsync();

        provider.Find("#dept-name").GetAttribute("value").Should().Be("Business Enablement");
    }

    [Fact]
    public async Task EditDepartmentDialog_RendersTheEightSwatches()
    {
        var (provider, _) = await OpenAsync();

        provider.FindAll(".app-deptdialog__swatch").Should().HaveCount(8);
    }

    [Fact]
    public async Task EditDepartmentDialog_MarksTheCurrentSwatchAsChecked()
    {
        var (provider, _) = await OpenAsync();

        provider.FindAll(".app-deptdialog__swatch")
            .Count(s => s.GetAttribute("aria-checked") == "true")
            .Should().Be(1);
    }

    [Fact]
    public async Task EditDepartmentDialog_OnColourPick_MovesTheCheckedSwatch()
    {
        var (provider, _) = await OpenAsync();
        IElement red = provider.FindAll(".app-deptdialog__swatch")[3];

        await red.ClickAsync(new());

        provider.FindAll(".app-deptdialog__swatch")[3]
            .GetAttribute("aria-checked").Should().Be("true");
    }

    [Fact]
    public async Task EditDepartmentDialog_WithABlankName_KeepsSaveDisabled()
    {
        var (provider, _) = await OpenAsync();

        await provider.Find("#dept-name").InputAsync(new() { Value = "  " });

        SaveButton(provider).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task EditDepartmentDialog_WithADuplicateName_StaysOpenAndShowsTheError()
    {
        var (provider, result) = await OpenAsync();

        await provider.Find("#dept-name").InputAsync(new() { Value = "Sales" });
        await SaveButton(provider).ClickAsync(new());

        provider.Markup.Should().Contain("already exists");
        result.IsCompleted.Should().BeFalse("a rejected save must leave the modal open");
    }

    [Fact]
    public async Task EditDepartmentDialog_SavingWithoutRenaming_Succeeds()
    {
        // The classic uniqueness bug: without excluding its own id, saving an
        // unchanged department fails with "name already taken".
        var (provider, result) = await OpenAsync();

        await SaveButton(provider).ClickAsync(new());

        (await result).Should().Be(DepartmentDialogOutcome.Saved);
    }

    [Fact]
    public async Task EditDepartmentDialog_OnSave_PersistsTheNewName()
    {
        var (provider, result) = await OpenAsync();

        await provider.Find("#dept-name").InputAsync(new() { Value = "Enablement" });
        await SaveButton(provider).ClickAsync(new());
        (await result).Should().Be(DepartmentDialogOutcome.Saved);

        DepartmentDetailDto? saved =
            await Services.GetRequiredService<IDepartmentService>().GetByIdAsync(6);
        saved!.Name.Should().Be("Enablement");
    }

    [Fact]
    public async Task EditDepartmentDialog_OnCancel_WritesNothing()
    {
        var (provider, result) = await OpenAsync();
        await provider.Find("#dept-name").InputAsync(new() { Value = "Discarded" });

        await CancelButton(provider).ClickAsync(new());

        (await result).Should().Be(DepartmentDialogOutcome.Cancelled);
        DepartmentDetailDto? unchanged =
            await Services.GetRequiredService<IDepartmentService>().GetByIdAsync(6);
        unchanged!.Name.Should().Be("Business Enablement");
    }

    [Fact]
    public async Task EditDepartmentDialog_WhenTheSaveFails_StaysOpen()
    {
        Services.GetRequiredService<TeamPortal.Mock.MockOptions>().FailNextWrite = true;
        var (provider, result) = await OpenAsync();

        await SaveButton(provider).ClickAsync(new());

        result.IsCompleted.Should().BeFalse();
        provider.FindAll("[role=alert]").Should().NotBeEmpty();
    }

    [Fact]
    public async Task EditDepartmentDialog_PutsSaveOnTheLeftAndCancelOnTheRight()
    {
        // Mandated by Frontend/TeamConventions.md, Dialogs & Modals.
        var (provider, _) = await OpenAsync();

        SaveButton(provider).TextContent.Should().Contain("Save");
        CancelButton(provider).TextContent.Should().Contain("Cancel");
    }
}

public class AddEmployeeDialogTests : DialogTestBase
{
    private async Task<(IRenderedComponent<MudDialogProvider> Provider, Task<bool> Result)>
        OpenAsync(int? defaultDepartmentId = null)
    {
        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        Task<bool> result = AddEmployeeDialog.ShowAsync(
            Dialogs, defaultDepartmentId: defaultDepartmentId);
        provider.WaitForAssertion(() => provider.FindAll("#emp-name").Should().NotBeEmpty());
        await Task.Yield();
        return (provider, result);
    }

    [Fact]
    public async Task AddEmployeeDialog_OnOpen_SaveIsDisabled()
    {
        var (provider, _) = await OpenAsync();

        SaveButton(provider).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task AddEmployeeDialog_WithANameAndDepartment_EnablesSave()
    {
        var (provider, _) = await OpenAsync();

        await provider.Find("#emp-name").InputAsync(new() { Value = "Nomsa" });

        SaveButton(provider).HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task AddEmployeeDialog_OffersBothStaffTypes()
    {
        var (provider, _) = await OpenAsync();

        provider.Markup.Should().Contain("Employee").And.Contain("Management");
    }

    [Fact]
    public async Task AddEmployeeDialog_OnSave_CreatesTheEmployee()
    {
        var (provider, result) = await OpenAsync(defaultDepartmentId: 6);
        IEmployeeService employees = Services.GetRequiredService<IEmployeeService>();
        int before = (await employees.GetAllAsync()).Count;

        await provider.Find("#emp-name").InputAsync(new() { Value = "Nomsa" });
        await provider.Find("#emp-role").InputAsync(new() { Value = "Data Engineer" });
        await SaveButton(provider).ClickAsync(new());

        (await result).Should().BeTrue();
        (await employees.GetAllAsync()).Should().HaveCount(before + 1);
    }

    [Fact]
    public async Task AddEmployeeDialog_OnCancel_CreatesNothing()
    {
        var (provider, result) = await OpenAsync(defaultDepartmentId: 6);
        IEmployeeService employees = Services.GetRequiredService<IEmployeeService>();
        int before = (await employees.GetAllAsync()).Count;

        await provider.Find("#emp-name").InputAsync(new() { Value = "Discarded" });
        await CancelButton(provider).ClickAsync(new());

        (await result).Should().BeFalse();
        (await employees.GetAllAsync()).Should().HaveCount(before);
    }

    [Fact]
    public async Task AddEmployeeDialog_WhenTheSaveFails_StaysOpenAndShowsTheError()
    {
        var (provider, result) = await OpenAsync(defaultDepartmentId: 6);
        await provider.Find("#emp-name").InputAsync(new() { Value = "Nomsa" });
        Services.GetRequiredService<TeamPortal.Mock.MockOptions>().FailNextWrite = true;

        await SaveButton(provider).ClickAsync(new());

        result.IsCompleted.Should().BeFalse();
        provider.FindAll("[role=alert]").Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddEmployeeDialog_PreselectsTheSuppliedDepartment()
    {
        var (provider, result) = await OpenAsync(defaultDepartmentId: 1);

        await provider.Find("#emp-name").InputAsync(new() { Value = "Thabo" });
        await SaveButton(provider).ClickAsync(new());
        (await result).Should().BeTrue();

        IReadOnlyList<EmployeeCardDto> sales =
            await Services.GetRequiredService<IEmployeeService>().GetByDepartmentAsync(1);
        sales.Should().ContainSingle(c => c.FullName == "Thabo");
    }
}
