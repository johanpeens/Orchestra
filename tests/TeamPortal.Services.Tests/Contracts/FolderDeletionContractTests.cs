using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Contracts;

public abstract class FolderDeletionContractTests : IDisposable
{
    protected FolderDeletionContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    // --- What is in there ----------------------------------------------------

    [Fact]
    public async Task GetContentsSummaryAsync_CountsTheProcessesAndTheSteps()
    {
        // Client Onboarding is the seeded folder: seven processes, and four
        // steps under the first of them.
        int folderId = await Fixture.OnboardingFolderIdAsync();

        FolderContentsDto summary = (await Fixture.Folders.GetContentsSummaryAsync(folderId))!;

        summary.Name.Should().Be("Client Onboarding");
        summary.ProcessCount.Should().Be(7);
        summary.StepCount.Should().Be(4);
        summary.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task GetContentsSummaryAsync_ReadsAsASentence()
    {
        // It goes straight into the confirmation, so it has to.
        int folderId = await Fixture.OnboardingFolderIdAsync();

        FolderContentsDto summary = (await Fixture.Folders.GetContentsSummaryAsync(folderId))!;

        summary.Summary.Should().Be("7 processes and 4 steps");
    }

    [Fact]
    public async Task GetContentsSummaryAsync_ForAnEmptyFolder_SaysSo()
    {
        Result<int> created = await Fixture.Folders.CreateAsync(
            new CreateProcessFolderRequest("Empty Shelf"));

        FolderContentsDto summary = (await Fixture.Folders.GetContentsSummaryAsync(created.Value))!;

        summary.IsEmpty.Should().BeTrue();
        summary.ProcessCount.Should().Be(0);
    }

    [Fact]
    public async Task GetContentsSummaryAsync_ForAFolderThatIsNotThere_ReturnsNull()
    {
        (await Fixture.Folders.GetContentsSummaryAsync(999_999)).Should().BeNull();
    }

    // --- The safe delete -----------------------------------------------------

    [Fact]
    public async Task DeleteAsync_WithProcessesInIt_IsStillRefused()
    {
        // Unchanged, and that is the point: the cascade is a separate call.
        int folderId = await Fixture.OnboardingFolderIdAsync();

        Result result = await Fixture.Folders.DeleteAsync(folderId);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_OnAnEmptyFolder_Succeeds()
    {
        Result<int> created = await Fixture.Folders.CreateAsync(
            new CreateProcessFolderRequest("Empty Shelf"));

        Result result = await Fixture.Folders.DeleteAsync(created.Value);

        result.IsSuccess.Should().BeTrue();
    }

    // --- The cascade ---------------------------------------------------------

    [Fact]
    public async Task DeleteWithEverythingInsideAsync_RemovesTheFolder()
    {
        int folderId = await Fixture.OnboardingFolderIdAsync();

        Result result = await Fixture.Folders.DeleteWithEverythingInsideAsync(folderId);

        result.IsSuccess.Should().BeTrue();

        IReadOnlyList<ProcessFolderDto> left = await Fixture.Folders.GetWithCountsAsync();
        left.Should().NotContain(f => f.Id == folderId);
    }

    [Fact]
    public async Task DeleteWithEverythingInsideAsync_TakesTheProcessesWithIt()
    {
        int folderId = await Fixture.OnboardingFolderIdAsync();

        await Fixture.Folders.DeleteWithEverythingInsideAsync(folderId);

        (await Fixture.Processes.GetByFolderAsync(folderId)).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteWithEverythingInsideAsync_TakesTheStepsWithItToo()
    {
        // The whole reason the cascade exists. A step whose process is gone is
        // unreachable but still in the database, counted by every report.
        int folderId = await Fixture.OnboardingFolderIdAsync();
        int processId = await Fixture.FirstProcessIdAsync();

        (await Fixture.Steps.GetByProcessAsync(processId)).Should().NotBeEmpty();

        await Fixture.Folders.DeleteWithEverythingInsideAsync(folderId);

        (await Fixture.Steps.GetByProcessAsync(processId)).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteWithEverythingInsideAsync_LeavesOtherFoldersAlone()
    {
        IReadOnlyList<ProcessFolderDto> before = await Fixture.Folders.GetWithCountsAsync();
        int folderId = await Fixture.OnboardingFolderIdAsync();

        await Fixture.Folders.DeleteWithEverythingInsideAsync(folderId);

        IReadOnlyList<ProcessFolderDto> after = await Fixture.Folders.GetWithCountsAsync();

        after.Should().HaveCount(before.Count - 1);
    }

    [Fact]
    public async Task DeleteWithEverythingInsideAsync_OnAnEmptyFolder_JustWorks()
    {
        // No special case for the empty folder. The caller should not have to
        // pick the right method based on what happens to be inside.
        Result<int> created = await Fixture.Folders.CreateAsync(
            new CreateProcessFolderRequest("Empty Shelf"));

        Result result = await Fixture.Folders.DeleteWithEverythingInsideAsync(created.Value);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteWithEverythingInsideAsync_ForAFolderThatIsNotThere_ReturnsNotFound()
    {
        Result result = await Fixture.Folders.DeleteWithEverythingInsideAsync(999_999);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteWithEverythingInsideAsync_LeavesTheActivityLogIntact()
    {
        // History outlives the record. The log is how anyone finds out where a
        // folder full of work went.
        int folderId = await Fixture.OnboardingFolderIdAsync();

        await Fixture.Folders.DeleteWithEverythingInsideAsync(folderId);

        PagedResult<ActivityLogEntryDto> log = await Fixture.ActivityLog.GetRecentAsync();

        log.Items.Should().Contain(e => e.EntityName == "ProcessFolder");
    }

    [Fact]
    public async Task DeleteWithEverythingInsideAsync_SaysWhatItRemovedInTheLog()
    {
        int folderId = await Fixture.OnboardingFolderIdAsync();

        await Fixture.Folders.DeleteWithEverythingInsideAsync(folderId);

        PagedResult<ActivityLogEntryDto> log = await Fixture.ActivityLog.GetRecentAsync();

        log.Items.Should().Contain(e =>
            e.EntityName == "ProcessFolder" && e.Summary.Contains("Client Onboarding"));
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Part A: against the in-memory implementation.</summary>
public sealed class MockFolderDeletionTests : FolderDeletionContractTests
{
    protected override ServiceFixture CreateFixture() => new MockServiceFixture();
}

/// <summary>Part B: the same contract, against EF Core over SQLite.</summary>
public sealed class SqlFolderDeletionTests : FolderDeletionContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}
