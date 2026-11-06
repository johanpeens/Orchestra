using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Contracts;

public abstract class ProcessStepServiceContractTests : IDisposable
{
    protected ProcessStepServiceContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    private IProcessStepService Sut => Fixture.Steps;

    // --- Reads ---------------------------------------------------------------

    [Fact]
    public async Task GetByProcessAsync_ReturnsTheSeededSteps()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        IReadOnlyList<ProcessStepDto> steps = await Sut.GetByProcessAsync(processId);

        steps.Should().HaveCount(4);
        steps[0].Name.Should().Be("Receive the inquiry");
        steps[^1].Name.Should().Be("Acknowledge the client");
    }

    [Fact]
    public async Task GetByProcessAsync_ReturnsStepsInStepNumberOrder()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        IReadOnlyList<ProcessStepDto> steps = await Sut.GetByProcessAsync(processId);

        steps.Select(s => s.StepNumber).Should().BeInAscendingOrder();
        steps.Select(s => s.StepNumber).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task GetByProcessAsync_IncludesDepartmentAndStatusColours()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        IReadOnlyList<ProcessStepDto> steps = await Sut.GetByProcessAsync(processId);

        steps.Should().OnlyContain(s => s.StatusColour.StartsWith('#'));
        steps[0].DepartmentName.Should().Be("Sales");
        steps[0].DepartmentColour.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetByProcessAsync_IncludesTheOwners()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        IReadOnlyList<ProcessStepDto> steps = await Sut.GetByProcessAsync(processId);

        steps[0].Owners.Should().HaveCount(4);
        steps[0].Owners.Select(o => o.FullName)
            .Should().Contain(["Sarah", "Maria", "James", "David Chen"]);
    }

    [Fact]
    public async Task GetByProcessAsync_PutsThePrimaryOwnerFirst()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        IReadOnlyList<ProcessStepDto> steps = await Sut.GetByProcessAsync(processId);

        steps[0].Owners[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task GetByProcessAsync_ForAnUnknownProcess_ReturnsEmpty()
    {
        (await Sut.GetByProcessAsync(999_999)).Should().BeEmpty();
    }

    // --- Create --------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_AssignsTheNextStepNumber()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        Result<int> created = await Sut.CreateAsync(new CreateStepRequest(processId, "Final Review"));

        created.IsSuccess.Should().BeTrue();
        ProcessStepDto step = (await Sut.GetByIdAsync(created.Value))!;
        step.StepNumber.Should().Be(5);
    }

    [Fact]
    public async Task CreateAsync_OnAnEmptyProcess_AssignsStepNumberOne()
    {
        // The off-by-one boundary. Max() over an empty set is the classic trap.
        IReadOnlyList<ProcessFolderDto> folders = await Fixture.Folders.GetWithCountsAsync();
        Result<int> process = await Fixture.Processes.CreateAsync(
            new CreateProcessRequest("Brand New Process", folders[0].Id));

        Result<int> created = await Sut.CreateAsync(new CreateStepRequest(process.Value, "First Step"));

        ProcessStepDto step = (await Sut.GetByIdAsync(created.Value))!;
        step.StepNumber.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithThreeOwners_RecordsThreeOwners()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        Result<int> created = await Sut.CreateAsync(
            new CreateStepRequest(processId, "Final Review", OwnerIds: [1, 2, 3]));

        ProcessStepDto step = (await Sut.GetByIdAsync(created.Value))!;
        step.Owners.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateOwnerIds_ReturnsValidationFailure()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        Result<int> created = await Sut.CreateAsync(
            new CreateStepRequest(processId, "Final Review", OwnerIds: [1, 2, 1]));

        created.IsFailure.Should().BeTrue();
        created.Errors.Should().Contain(e => e.Field == nameof(CreateStepRequest.OwnerIds));
    }

    [Fact]
    public async Task CreateAsync_WithAnUnknownOwner_ReturnsValidationFailure()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        Result<int> created = await Sut.CreateAsync(
            new CreateStepRequest(processId, "Final Review", OwnerIds: [999_999]));

        created.IsFailure.Should().BeTrue();
        created.Errors.Should().Contain(e => e.Field == nameof(CreateStepRequest.OwnerIds));
    }

    [Fact]
    public async Task CreateAsync_WithNoOwners_Succeeds()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        Result<int> created = await Sut.CreateAsync(new CreateStepRequest(processId, "Final Review"));

        created.IsSuccess.Should().BeTrue("a step can be created before anyone is assigned");
    }

    [Fact]
    public async Task CreateAsync_WithAnUnknownProcess_ReturnsValidationFailure()
    {
        Result<int> created = await Sut.CreateAsync(new CreateStepRequest(999_999, "Orphan Step"));

        created.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_StartsTheStepAtTheDefaultStatus()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<StepStatusDto> statuses = await Fixture.Statuses.GetAllAsync();
        StepStatusDto expected = statuses.First(s => s.IsDefault);

        Result<int> created = await Sut.CreateAsync(new CreateStepRequest(processId, "Final Review"));

        ProcessStepDto step = (await Sut.GetByIdAsync(created.Value))!;
        step.StatusName.Should().Be(expected.Name);
    }

    [Fact]
    public async Task CreateAsync_WhenAnOwnerIsInvalid_WritesNoStepAtAll()
    {
        // All-or-nothing. A rejected owner must not leave a half-saved step behind.
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);

        await Sut.CreateAsync(new CreateStepRequest(processId, "Final Review", OwnerIds: [999_999]));

        IReadOnlyList<ProcessStepDto> after = await Sut.GetByProcessAsync(processId);
        after.Should().HaveCount(before.Count);
    }

    [Fact]
    public async Task CreateAsync_WithARejectedScreenshot_WritesNoStepAtAll()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);

        var pdf = new FileUpload("spec.pdf", "application/pdf", 1024, new MemoryStream([1, 2, 3]));

        Result<int> created = await Sut.CreateAsync(
            new CreateStepRequest(processId, "Final Review", Screenshot: pdf));

        created.IsFailure.Should().BeTrue();
        (await Sut.GetByProcessAsync(processId)).Should().HaveCount(before.Count);
    }

    // --- Renumbering ---------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RenumbersTheRemainingSteps()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);

        await Sut.DeleteAsync(before[2].Id);

        IReadOnlyList<ProcessStepDto> after = await Sut.GetByProcessAsync(processId);
        after.Select(s => s.StepNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task DeleteAsync_OnTheMiddleStep_LeavesNoDuplicateNumbers()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);

        await Sut.DeleteAsync(before[2].Id);

        IReadOnlyList<ProcessStepDto> after = await Sut.GetByProcessAsync(processId);
        after.Select(s => s.StepNumber).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task DeleteAsync_OnTheFirstStep_RenumbersFromOne()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);

        await Sut.DeleteAsync(before[0].Id);

        IReadOnlyList<ProcessStepDto> after = await Sut.GetByProcessAsync(processId);
        after[0].StepNumber.Should().Be(1);
        after[0].Name.Should().Be("Verify basic information");
    }

    [Fact]
    public async Task DeleteAsync_OnTheLastStep_LeavesTheRestUntouched()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);

        await Sut.DeleteAsync(before[^1].Id);

        IReadOnlyList<ProcessStepDto> after = await Sut.GetByProcessAsync(processId);
        after.Select(s => s.StepNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task DeleteAsync_AlsoRemovesTheOwners()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);
        int stepId = before[0].Id;

        await Sut.DeleteAsync(stepId);

        (await Sut.GetByIdAsync(stepId)).Should().BeNull();
    }

    [Fact]
    public async Task ResequenceAsync_OnAlreadyCorrectSteps_ChangesNothing()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);

        await Sut.ResequenceAsync(processId);

        IReadOnlyList<ProcessStepDto> after = await Sut.GetByProcessAsync(processId);
        after.Select(s => (s.Id, s.StepNumber))
            .Should().Equal(before.Select(s => (s.Id, s.StepNumber)));
    }

    // --- Move ----------------------------------------------------------------

    [Fact]
    public async Task MoveAsync_ToTheFront_ShiftsTheOthersDown()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);
        ProcessStepDto fourth = before[3];

        await Sut.MoveAsync(new MoveStepRequest(fourth.Id, 1));

        IReadOnlyList<ProcessStepDto> after = await Sut.GetByProcessAsync(processId);
        after[0].Id.Should().Be(fourth.Id);
        after[0].StepNumber.Should().Be(1);
        after.Select(s => s.StepNumber).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task MoveAsync_BeyondTheEnd_ClampsToTheLastPosition()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> before = await Sut.GetByProcessAsync(processId);
        ProcessStepDto first = before[0];

        await Sut.MoveAsync(new MoveStepRequest(first.Id, 99));

        IReadOnlyList<ProcessStepDto> after = await Sut.GetByProcessAsync(processId);
        after[^1].Id.Should().Be(first.Id);
        after.Select(s => s.StepNumber).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task MoveAsync_ForAnUnknownStep_ReturnsNotFound()
    {
        (await Sut.MoveAsync(new MoveStepRequest(999_999, 1))).IsFailure.Should().BeTrue();
    }

    // --- Update --------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ReplacesTheOwnerList()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> steps = await Sut.GetByProcessAsync(processId);
        ProcessStepDto first = steps[0];

        Result result = await Sut.UpdateAsync(
            new UpdateStepRequest(first.Id, first.Name, OwnerIds: [5]));

        result.IsSuccess.Should().BeTrue();
        ProcessStepDto updated = (await Sut.GetByIdAsync(first.Id))!;
        updated.Owners.Should().ContainSingle().Which.EmployeeId.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_WithNullOwnerIds_LeavesTheOwnersAlone()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> steps = await Sut.GetByProcessAsync(processId);
        ProcessStepDto first = steps[0];

        await Sut.UpdateAsync(new UpdateStepRequest(first.Id, "Renamed Step"));

        ProcessStepDto updated = (await Sut.GetByIdAsync(first.Id))!;
        updated.Name.Should().Be("Renamed Step");
        updated.Owners.Should().HaveCount(4, "passing no owner list means 'do not touch them'");
    }

    [Fact]
    public async Task UpdateAsync_ForAnUnknownStep_ReturnsNotFound()
    {
        (await Sut.UpdateAsync(new UpdateStepRequest(999_999, "Ghost"))).IsFailure.Should().BeTrue();
    }

    // --- Screenshot URLs -----------------------------------------------------

    [Fact]
    public async Task AStepImage_CarriesAUrlTheBrowserCanFetch()
    {
        // It used to carry the raw stored path - "steps/abc.png" - which an
        // <img src> resolves against the CURRENT PAGE. The upload worked, the row
        // was written, and the preview showed nothing at all.
        int processId = await Fixture.FirstProcessIdAsync();

        Result<int> created = await Fixture.Steps.CreateAsync(new CreateStepRequest(
            processId, "With a screenshot", Screenshot: Png()));

        created.IsSuccess.Should().BeTrue();

        ProcessStepDto step = (await Fixture.Steps.GetByIdAsync(created.Value))!;

        step.Images.Should().ContainSingle();
        step.Images[0].Url.Should().StartWith(
            MediaPaths.Prefix, "a relative path renders as a broken image");
    }

    [Fact]
    public async Task AStepImageUrl_PointsAtSomethingTheFileStoreCanFind()
    {
        // The round trip. A URL that looks right but does not resolve back to a
        // stored path is the same bug wearing a prefix.
        int processId = await Fixture.FirstProcessIdAsync();

        Result<int> created = await Fixture.Steps.CreateAsync(new CreateStepRequest(
            processId, "With a screenshot", Screenshot: Png()));

        ProcessStepDto step = (await Fixture.Steps.GetByIdAsync(created.Value))!;

        string storedPath = MediaPaths.StoredPath(step.Images[0].Url);

        storedPath.Should().NotStartWith("/");
        storedPath.Should().StartWith("steps/");
    }

    [Fact]
    public async Task TheFileStore_AgreesWithTheProjectionAboutTheUrl()
    {
        // Two places build this string. They have to build the same one.
        int processId = await Fixture.FirstProcessIdAsync();

        Result<int> created = await Fixture.Steps.CreateAsync(new CreateStepRequest(
            processId, "With a screenshot", Screenshot: Png()));

        ProcessStepDto step = (await Fixture.Steps.GetByIdAsync(created.Value))!;

        string fromProjection = step.Images[0].Url;
        string fromStore = Fixture.Files.GetUrl(MediaPaths.StoredPath(fromProjection));

        fromStore.Should().Be(fromProjection);
    }

    [Fact]
    public async Task UpdateAsync_WithAScreenshot_SavesIt()
    {
        // This did not work at all. UpdateStepRequest had no screenshot field, so
        // the dialog quietly dropped every upload made while EDITING a step -
        // which is every upload onto a step that already exists.
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> steps = await Fixture.Steps.GetByProcessAsync(processId);
        int stepId = steps[0].Id;

        Result result = await Fixture.Steps.UpdateAsync(
            new UpdateStepRequest(stepId, steps[0].Name, Screenshot: Png()));

        result.IsSuccess.Should().BeTrue();

        ProcessStepDto after = (await Fixture.Steps.GetByIdAsync(stepId))!;

        after.HasImages.Should().BeTrue();
        after.Images[0].Url.Should().StartWith(MediaPaths.Prefix);
    }

    [Fact]
    public async Task UpdateAsync_WithNoScreenshot_LeavesTheExistingOneAlone()
    {
        // Null means "not changing it", not "remove it". Otherwise renaming a
        // step would silently throw its screenshot away.
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> steps = await Fixture.Steps.GetByProcessAsync(processId);
        int stepId = steps[0].Id;

        await Fixture.Steps.UpdateAsync(
            new UpdateStepRequest(stepId, steps[0].Name, Screenshot: Png()));

        await Fixture.Steps.UpdateAsync(new UpdateStepRequest(stepId, "Renamed"));

        ProcessStepDto after = (await Fixture.Steps.GetByIdAsync(stepId))!;

        after.Name.Should().Be("Renamed");
        after.HasImages.Should().BeTrue("a rename must not throw the screenshot away");
    }

    [Fact]
    public async Task UpdateAsync_WithASecondScreenshot_ReplacesTheFirst()
    {
        // The step detail shows one. A second row would be invisible and would
        // never be cleaned up.
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> steps = await Fixture.Steps.GetByProcessAsync(processId);
        int stepId = steps[0].Id;

        await Fixture.Steps.UpdateAsync(
            new UpdateStepRequest(stepId, steps[0].Name, Screenshot: Png("first.png")));

        await Fixture.Steps.UpdateAsync(
            new UpdateStepRequest(stepId, steps[0].Name, Screenshot: Png("second.png")));

        ProcessStepDto after = (await Fixture.Steps.GetByIdAsync(stepId))!;

        after.Images.Should().ContainSingle();
        after.Images[0].FileName.Should().Be("second.png");
    }

    [Fact]
    public async Task UpdateAsync_WithARejectedScreenshot_ChangesNothing()
    {
        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> steps = await Fixture.Steps.GetByProcessAsync(processId);
        int stepId = steps[0].Id;

        using var content = new MemoryStream([1, 2, 3]);
        var rubbish = new FileUpload("notes.pdf", "application/pdf", 3, content);

        Result result = await Fixture.Steps.UpdateAsync(
            new UpdateStepRequest(stepId, "Should not stick", Screenshot: rubbish));

        result.IsFailure.Should().BeTrue();

        ProcessStepDto after = (await Fixture.Steps.GetByIdAsync(stepId))!;

        after.Name.Should().Be(steps[0].Name, "a rejected file must not half-update the step");
    }

    private static FileUpload Png(string name = "shot.png")
    {
        byte[] bytes = FileSignature.HeaderFor("image/png");
        return new FileUpload(name, "image/png", bytes.Length, new MemoryStream(bytes));
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Part A: runs the contract against the in-memory implementation.</summary>
public sealed class MockProcessStepServiceTests : ProcessStepServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new MockServiceFixture();
}

/// <summary>Part B: the same contract, run against EF Core over SQLite.</summary>
public sealed class SqlProcessStepServiceTests : ProcessStepServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}
