using Bunit;
using MudBlazor;
using TeamPortal.Web.Components.Shared;

namespace TeamPortal.Web.Tests.Screens;

public class ConfirmDialogTests : DialogTestBase
{
    private async Task<(IRenderedComponent<MudDialogProvider> Provider, Task<bool> Result)>
        OpenAsync(string? warning = null)
    {
        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        Task<bool> result = ConfirmDialog.ShowAsync(
            Dialogs, "Delete 'Onboarding Process'?", "Delete process", warning);

        provider.WaitForAssertion(() =>
            provider.FindAll(".mud-dialog-actions button").Should().NotBeEmpty());
        await Task.Yield();
        return (provider, result);
    }

    [Fact]
    public async Task ConfirmDialog_ShowsTheMessage()
    {
        var (provider, _) = await OpenAsync();

        provider.Markup.Should().Contain("Delete 'Onboarding Process'?");
    }

    [Fact]
    public async Task ConfirmDialog_WithAWarning_ShowsIt()
    {
        var (provider, _) = await OpenAsync("Every step will be removed with it.");

        provider.Markup.Should().Contain("Every step will be removed with it.");
    }

    [Fact]
    public async Task ConfirmDialog_WithNoWarning_ShowsNone()
    {
        var (provider, _) = await OpenAsync();

        provider.FindAll(".mud-alert").Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmDialog_OnConfirm_ReturnsTrue()
    {
        var (provider, result) = await OpenAsync();

        await SaveButton(provider).ClickAsync(new());

        (await result).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmDialog_OnCancel_ReturnsFalse()
    {
        var (provider, result) = await OpenAsync();

        await CancelButton(provider).ClickAsync(new());

        (await result).Should().BeFalse("cancelling must never be read as consent");
    }

    [Fact]
    public async Task ConfirmDialog_PutsTheConfirmOnTheLeftAndCancelOnTheRight()
    {
        var (provider, _) = await OpenAsync();

        SaveButton(provider).TextContent.Should().Contain("Delete");
        CancelButton(provider).TextContent.Should().Contain("Cancel");
    }
}
