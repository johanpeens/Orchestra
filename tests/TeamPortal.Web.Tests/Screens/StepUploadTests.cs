using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Application.Validators;
using TeamPortal.Web.Components.Dialogs;

namespace TeamPortal.Web.Tests.Screens;

public class StepUploadTests : DialogTestBase
{
    // Process 1 is "Initial Contact" - the only seeded process with steps.
    private const int OnboardingProcessId = 1;

    private IProcessStepService StepService => Services.GetRequiredService<IProcessStepService>();

    /// <summary>
    /// Minimal valid file headers. Duplicated here as test data on purpose - the
    /// production check lives inside TeamPortal.Mock and stays internal, and a
    /// test that reuses the implementation's own constants proves nothing.
    /// </summary>
    private static byte[] HeaderFor(string contentType) => contentType switch
    {
        "image/png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0],
        "image/jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0],
        "image/webp" => [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50],
        _ => [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
    };

    private static FileUpload Image(
        string name = "shot.png", string contentType = "image/png", byte[]? content = null)
    {
        byte[] bytes = content ?? HeaderFor(contentType);
        return new FileUpload(name, contentType, bytes.Length, new MemoryStream(bytes));
    }

    // --- The dialog renders the upload affordance --------------------------------

    [Fact]
    public async Task AddStepDialog_RendersTheDropZoneAsAButton()
    {
        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        _ = AddStepDialog.ShowAsync(Dialogs, OnboardingProcessId);
        provider.WaitForAssertion(() => provider.FindAll(".app-stepdialog__drop").Should().NotBeEmpty());
        await Task.Yield();

        var drop = provider.Find(".app-stepdialog__drop");
        drop.GetAttribute("role").Should().Be("button");
        drop.GetAttribute("aria-label").Should().Be("Upload a reference screenshot");
    }

    [Fact]
    public async Task AddStepDialog_InEditMode_ListsAlreadyAttachedImages()
    {
        // Attach one first, then reopen the dialog on that step.
        Result<int> created = await StepService.CreateAsync(
            new CreateStepRequest(OnboardingProcessId, "With Screenshot", Screenshot: Image()));
        created.IsSuccess.Should().BeTrue();

        IRenderedComponent<MudDialogProvider> provider = OpenProvider();
        _ = AddStepDialog.ShowAsync(Dialogs, OnboardingProcessId, created.Value);
        provider.WaitForAssertion(() => provider.FindAll("#step-name").Should().NotBeEmpty());
        await Task.Yield();

        provider.Markup.Should().Contain("Already attached").And.Contain("shot.png");
    }

    // --- The save path actually stores the file -----------------------------------

    [Fact]
    public async Task CreateStep_WithAValidPng_AttachesTheImage()
    {
        Result<int> created = await StepService.CreateAsync(
            new CreateStepRequest(OnboardingProcessId, "With Screenshot", Screenshot: Image()));

        created.IsSuccess.Should().BeTrue();
        ProcessStepDto? step = await StepService.GetByIdAsync(created.Value);
        step!.HasImages.Should().BeTrue();
        step.Images[0].FileName.Should().Be("shot.png");
    }

    [Fact]
    public async Task CreateStep_TheStoredPathIsGeneratedNotTheCallersFilename()
    {
        Result<int> created = await StepService.CreateAsync(
            new CreateStepRequest(
                OnboardingProcessId, "Traversal Attempt",
                Screenshot: Image(name: "../../../web.config")));

        created.IsSuccess.Should().BeTrue();
        ProcessStepDto? step = await StepService.GetByIdAsync(created.Value);

        step!.Images[0].Url.Should().NotContain("..");
        step.Images[0].Url.Should().NotContain("web.config");
        step.Images[0].FileName.Should().Be("../../../web.config",
            "the original name is kept for display only, never used on disk");
    }

    [Fact]
    public async Task CreateStep_WithAPdfRenamedToPng_IsRejected()
    {
        // The declared type and the extension both say PNG; the bytes do not.
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0, 0, 0, 0];

        Result<int> created = await StepService.CreateAsync(
            new CreateStepRequest(
                OnboardingProcessId, "Sneaky Upload", Screenshot: Image(content: pdf)));

        created.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateStep_WithAnOversizeImage_IsRejected()
    {
        var big = new FileUpload(
            "big.png", "image/png", FileUploadValidator.MaxSizeBytes + 1,
            new MemoryStream(HeaderFor("image/png")));

        Result<int> created = await StepService.CreateAsync(
            new CreateStepRequest(OnboardingProcessId, "Too Big", Screenshot: big));

        created.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateStep_WhenTheImageIsRejected_NoStepIsLeftBehind()
    {
        // All-or-nothing. A rejected screenshot must not leave a half-made step.
        int before = (await StepService.GetByProcessAsync(OnboardingProcessId)).Count;
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0, 0, 0, 0, 0, 0, 0, 0];

        await StepService.CreateAsync(new CreateStepRequest(
            OnboardingProcessId, "Sneaky Upload", Screenshot: Image(content: pdf)));

        (await StepService.GetByProcessAsync(OnboardingProcessId)).Should().HaveCount(before);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public async Task CreateStep_AcceptsEveryAllowedImageType(string contentType)
    {
        Result<int> created = await StepService.CreateAsync(
            new CreateStepRequest(
                OnboardingProcessId, $"Step {contentType}",
                Screenshot: Image($"shot{contentType.Length}", contentType)));

        created.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateStep_WithNoScreenshot_StillSucceeds()
    {
        Result<int> created = await StepService.CreateAsync(
            new CreateStepRequest(OnboardingProcessId, "No Screenshot"));

        created.IsSuccess.Should().BeTrue();
        ProcessStepDto? step = await StepService.GetByIdAsync(created.Value);
        step!.HasImages.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteStep_AlsoRemovesItsImages()
    {
        Result<int> created = await StepService.CreateAsync(
            new CreateStepRequest(OnboardingProcessId, "With Screenshot", Screenshot: Image()));

        await StepService.DeleteAsync(created.Value);

        (await StepService.GetByIdAsync(created.Value)).Should().BeNull();
    }
}
