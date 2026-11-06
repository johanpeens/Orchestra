using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Application.Validators;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Contracts;

public abstract class DepartmentServiceContractTests : IDisposable
{
    protected DepartmentServiceContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    [Fact]
    public async Task GetWithCountsAsync_CountsOnlyActiveEmployees()
    {
        IReadOnlyList<DepartmentListItemDto> departments =
            await Fixture.Departments.GetWithCountsAsync();

        DepartmentListItemDto enablement = departments.First(d => d.Name == "Business Enablement");
        enablement.EmployeeCount.Should().Be(8);
    }

    [Fact]
    public async Task GetWithCountsAsync_ForAnEmptyDepartment_ReturnsZeroNotNull()
    {
        IReadOnlyList<DepartmentListItemDto> departments =
            await Fixture.Departments.GetWithCountsAsync();

        departments.First(d => d.Name == "Underwriting").EmployeeCount.Should().Be(0);
    }

    [Fact]
    public async Task GetWithCountsAsync_ReflectsANewEmployeeImmediately()
    {
        // The count is calculated, never stored - so it can never drift.
        int enablement = await Fixture.EnablementDepartmentIdAsync();
        await Fixture.Employees.CreateAsync(new CreateEmployeeRequest("Nomsa", enablement));

        IReadOnlyList<DepartmentListItemDto> after = await Fixture.Departments.GetWithCountsAsync();

        after.First(d => d.Id == enablement).EmployeeCount.Should().Be(9);
    }

    [Fact]
    public async Task UpdateAsync_WithTheSameNameAsItself_Succeeds()
    {
        // The classic uniqueness bug. Without excluding its own id, saving a
        // department you did not rename fails with "name already taken".
        int enablement = await Fixture.EnablementDepartmentIdAsync();
        DepartmentDetailDto detail = (await Fixture.Departments.GetByIdAsync(enablement))!;

        Result result = await Fixture.Departments.UpdateAsync(
            new UpdateDepartmentRequest(detail.Id, detail.Name, BrandPalette.Aqua100));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WithAnotherDepartmentsName_Fails()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result result = await Fixture.Departments.UpdateAsync(
            new UpdateDepartmentRequest(enablement, "Sales", BrandPalette.Aqua100));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == nameof(UpdateDepartmentRequest.Name));
    }

    [Fact]
    public async Task UpdateAsync_IsCaseInsensitiveAboutDuplicateNames()
    {
        // SQL Server's default collation is case-insensitive. A mock that is not
        // would let a duplicate through in Part A that the database then rejects.
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result result = await Fixture.Departments.UpdateAsync(
            new UpdateDepartmentRequest(enablement, "sALES", BrandPalette.Aqua100));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WithABadColour_ReturnsValidationFailure()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result result = await Fixture.Departments.UpdateAsync(
            new UpdateDepartmentRequest(enablement, "Business Enablement", "purple"));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == nameof(UpdateDepartmentRequest.ColourHex));
    }

    [Fact]
    public async Task UpdateAsync_ChangingTheColour_RepaintsTheStepTags()
    {
        // Department colour is stored once and read everywhere. Changing it must
        // flow through to the step cards on screen 02 with no second write.
        IReadOnlyList<DepartmentListItemDto> departments =
            await Fixture.Departments.GetWithCountsAsync();
        DepartmentListItemDto sales = departments.First(d => d.Name == "Sales");

        await Fixture.Departments.UpdateAsync(
            new UpdateDepartmentRequest(sales.Id, sales.Name, BrandPalette.Red100));

        int processId = await Fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> steps = await Fixture.Steps.GetByProcessAsync(processId);

        steps.Where(s => s.DepartmentName == "Sales")
            .Should().OnlyContain(s => s.DepartmentColour == BrandPalette.Red100);
    }

    [Fact]
    public async Task DeleteAsync_WithEmployeesInIt_IsRefused()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result result = await Fixture.Departments.DeleteAsync(enablement);

        result.IsFailure.Should().BeTrue();
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class MockDepartmentServiceTests : DepartmentServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new MockServiceFixture();
}

/// <summary>Part B: the same contract, run against EF Core over SQLite.</summary>
public sealed class SqlDepartmentServiceTests : DepartmentServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}

public abstract class ProcessServiceContractTests : IDisposable
{
    protected ProcessServiceContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    [Fact]
    public async Task GetDetailAsync_ReturnsTheProcessWithItsSteps()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        ProcessDetailDto? detail = await Fixture.Processes.GetDetailAsync(processId);

        detail.Should().NotBeNull();
        detail!.Name.Should().Be("Initial Contact");
        detail.Steps.Should().HaveCount(4);
        detail.HasSteps.Should().BeTrue();
    }

    [Fact]
    public async Task GetDetailAsync_ForAnUnknownId_ReturnsNull()
    {
        (await Fixture.Processes.GetDetailAsync(999_999)).Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithADuplicateNameInTheSameFolder_Fails()
    {
        IReadOnlyList<ProcessFolderDto> folders = await Fixture.Folders.GetWithCountsAsync();
        int clientFolder = folders.First(f => f.Name == "Client Onboarding").Id;

        Result<int> result = await Fixture.Processes.CreateAsync(
            new CreateProcessRequest("Initial Contact", clientFolder));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == nameof(CreateProcessRequest.Name));
    }

    [Fact]
    public async Task CreateAsync_WithTheSameNameInAnotherFolder_Succeeds()
    {
        // Uniqueness is per folder, not global.
        IReadOnlyList<ProcessFolderDto> folders = await Fixture.Folders.GetWithCountsAsync();
        int otherFolder = folders.First(f => f.Name == "The Business").Id;

        Result<int> result = await Fixture.Processes.CreateAsync(
            new CreateProcessRequest("Initial Contact", otherFolder));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_SetsSortOrderToTheEndOfTheFolder()
    {
        IReadOnlyList<ProcessFolderDto> folders = await Fixture.Folders.GetWithCountsAsync();
        int clientFolder = folders.First(f => f.Name == "Client Onboarding").Id;

        await Fixture.Processes.CreateAsync(new CreateProcessRequest("Second Process", clientFolder));

        IReadOnlyList<ProcessListItemDto> list = await Fixture.Processes.GetByFolderAsync(clientFolder);
        list[^1].Name.Should().Be("Second Process");
    }

    [Fact]
    public async Task DeleteAsync_AlsoRemovesItsSteps()
    {
        int processId = await Fixture.FirstProcessIdAsync();

        Result result = await Fixture.Processes.DeleteAsync(processId);

        result.IsSuccess.Should().BeTrue();
        (await Fixture.Steps.GetByProcessAsync(processId)).Should().BeEmpty();
        (await Fixture.Processes.GetDetailAsync(processId)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_LeavesTheActivityLogIntact()
    {
        // History outlives the record it describes.
        int processId = await Fixture.FirstProcessIdAsync();
        await Fixture.Processes.UpdateAsync(new UpdateProcessRequest(processId, "Renamed"));

        await Fixture.Processes.DeleteAsync(processId);

        PagedResult<ActivityLogEntryDto> log =
            await Fixture.ActivityLog.GetForEntityAsync("Process", processId);

        log.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetWithCountsAsync_CountsOnlyLiveProcesses()
    {
        IReadOnlyList<ProcessFolderDto> before = await Fixture.Folders.GetWithCountsAsync();
        int clientFolder = before.First(f => f.Name == "Client Onboarding").Id;
        before.First(f => f.Id == clientFolder).ProcessCount.Should().Be(7);

        await Fixture.Processes.DeleteAsync(await Fixture.FirstProcessIdAsync());

        IReadOnlyList<ProcessFolderDto> after = await Fixture.Folders.GetWithCountsAsync();
        after.First(f => f.Id == clientFolder).ProcessCount.Should().Be(6);
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class MockProcessServiceTests : ProcessServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new MockServiceFixture();
}

/// <summary>Part B: the same contract, run against EF Core over SQLite.</summary>
public sealed class SqlProcessServiceTests : ProcessServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}

public abstract class AuthServiceContractTests : IDisposable
{
    protected AuthServiceContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    private const string Admin = "admin@company.com";

    [Fact]
    public async Task SignInAsync_WithCorrectCredentials_Succeeds()
    {
        SignInResultDto result = await Fixture.Auth.SignInAsync(
            new SignInRequest(Admin, Fixture.DemoPassword));

        result.Succeeded.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.Email.Should().Be(Admin);
    }

    [Fact]
    public async Task SignInAsync_OnSuccess_PopulatesTheCurrentUser()
    {
        await Fixture.Auth.SignInAsync(new SignInRequest(Admin, Fixture.DemoPassword));

        Fixture.Get<TeamPortal.Application.Abstractions.ICurrentUserService>()
            .IsSignedIn.Should().BeTrue();
    }

    [Fact]
    public async Task SignInAsync_WithTheWrongPassword_Fails()
    {
        SignInResultDto result = await Fixture.Auth.SignInAsync(new SignInRequest(Admin, "wrong"));

        result.Succeeded.Should().BeFalse();
        result.Reason.Should().Be(SignInFailureReason.InvalidCredentials);
    }

    [Fact]
    public async Task SignInAsync_WithAnUnknownEmail_GivesTheSameMessageAsAWrongPassword()
    {
        // Different messages here would let an attacker discover which addresses
        // have accounts, one guess at a time.
        SignInResultDto unknown = await Fixture.Auth.SignInAsync(
            new SignInRequest("nobody@company.com", "whatever"));

        SignInResultDto wrongPassword = await Fixture.Auth.SignInAsync(
            new SignInRequest(Admin, "wrong"));

        unknown.Message.Should().Be(wrongPassword.Message);
        unknown.Reason.Should().Be(wrongPassword.Reason);
    }

    [Fact]
    public async Task SignInAsync_AfterFiveFailures_ReportsLockedOut()
    {
        for (int i = 0; i < 4; i++)
        {
            await Fixture.Auth.SignInAsync(new SignInRequest(Admin, "wrong"));
        }

        SignInResultDto fifth = await Fixture.Auth.SignInAsync(new SignInRequest(Admin, "wrong"));

        fifth.Reason.Should().Be(SignInFailureReason.LockedOut);
        fifth.LockoutEnd.Should().NotBeNull();
    }

    [Fact]
    public async Task SignInAsync_WhenLockedOut_RejectsEvenTheCorrectPassword()
    {
        for (int i = 0; i < 5; i++)
        {
            await Fixture.Auth.SignInAsync(new SignInRequest(Admin, "wrong"));
        }

        SignInResultDto result = await Fixture.Auth.SignInAsync(
            new SignInRequest(Admin, Fixture.DemoPassword));

        result.Succeeded.Should().BeFalse("lockout has to be real, not cosmetic");
        result.Reason.Should().Be(SignInFailureReason.LockedOut);
    }

    [Fact]
    public async Task SignInAsync_AfterASuccess_ResetsTheFailureCount()
    {
        await Fixture.Auth.SignInAsync(new SignInRequest(Admin, "wrong"));
        await Fixture.Auth.SignInAsync(new SignInRequest(Admin, "wrong"));
        await Fixture.Auth.SignInAsync(new SignInRequest(Admin, Fixture.DemoPassword));

        // Three more failures must not trip the lockout if the counter reset.
        for (int i = 0; i < 3; i++)
        {
            await Fixture.Auth.SignInAsync(new SignInRequest(Admin, "wrong"));
        }

        SignInResultDto result = await Fixture.Auth.SignInAsync(
            new SignInRequest(Admin, Fixture.DemoPassword));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task SignInAsync_OnSuccess_WritesALoggedInActivityRow()
    {
        await Fixture.Auth.SignInAsync(new SignInRequest(Admin, Fixture.DemoPassword));

        PagedResult<ActivityLogEntryDto> log = await Fixture.ActivityLog.GetRecentAsync();

        log.Items.Should().Contain(e =>
            e.Action == TeamPortal.Application.Enums.ActivityAction.LoggedIn);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_ForAnUnknownEmail_StillSucceeds()
    {
        Result result = await Fixture.Auth.RequestPasswordResetAsync(
            new ForgotPasswordRequest("nobody@company.com"));

        result.IsSuccess.Should().BeTrue(
            "reporting 'no such account' would hand over a user enumeration tool");
    }

    [Fact]
    public async Task SignOutAsync_ClearsTheCurrentUser()
    {
        await Fixture.Auth.SignInAsync(new SignInRequest(Admin, Fixture.DemoPassword));

        await Fixture.Auth.SignOutAsync();

        Fixture.Get<TeamPortal.Application.Abstractions.ICurrentUserService>()
            .IsSignedIn.Should().BeFalse();
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class MockAuthServiceTests : AuthServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new MockServiceFixture();
}

/// <summary>
/// Part B: the same contract, run against real ASP.NET Core Identity. Phase B3.
/// If any of these go red, sign-in behaves differently from the screen's
/// expectations - and screen 01 has not changed since phase A4.
/// </summary>
public sealed class IdentityAuthServiceTests : AuthServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}

public abstract class FileStoreContractTests : IDisposable
{
    protected FileStoreContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    private static FileUpload Upload(
        string name, string contentType, long? size = null, byte[]? content = null)
    {
        byte[] bytes = content ?? FileSignature.HeaderFor(contentType);
        return new FileUpload(name, contentType, size ?? bytes.Length, new MemoryStream(bytes));
    }

    [Fact]
    public async Task SaveAsync_WithAValidPng_ReturnsAStoredPath()
    {
        Result<string> result = await Fixture.Files.SaveAsync(Upload("shot.png", "image/png"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SaveAsync_WithA6MbFile_IsRejected()
    {
        Result<string> result = await Fixture.Files.SaveAsync(
            Upload("big.png", "image/png", size: 6 * 1024 * 1024));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WithAPdfRenamedToPng_IsRejected()
    {
        // The extension and the declared type both say PNG. The bytes do not.
        // This is the check that stops payload.exe arriving as photo.png.
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0, 0, 0, 0];

        Result<string> result = await Fixture.Files.SaveAsync(
            Upload("photo.png", "image/png", content: pdfBytes));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WithADisallowedType_IsRejected()
    {
        Result<string> result = await Fixture.Files.SaveAsync(Upload("doc.pdf", "application/pdf"));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_GeneratesANewNameRatherThanReusingTheCallers()
    {
        Result<string> result = await Fixture.Files.SaveAsync(
            Upload("../../../web.config", "image/png"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain("..", "a caller's filename must never reach the filesystem");
        result.Value.Should().NotContain("web.config");
    }

    [Fact]
    public async Task SaveAsync_TwoFilesWithTheSameName_GetDifferentPaths()
    {
        Result<string> a = await Fixture.Files.SaveAsync(Upload("shot.png", "image/png"));
        Result<string> b = await Fixture.Files.SaveAsync(Upload("shot.png", "image/png"));

        b.Value.Should().NotBe(a.Value);
    }

    [Fact]
    public async Task SaveAsync_AtExactlyFiveMb_IsAccepted()
    {
        Result<string> result = await Fixture.Files.SaveAsync(
            Upload("shot.png", "image/png", size: FileUploadValidator.MaxSizeBytes));

        result.IsSuccess.Should().BeTrue("5 MB is the documented limit, so it must be allowed");
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class MockFileStoreTests : FileStoreContractTests
{
    protected override ServiceFixture CreateFixture() => new MockServiceFixture();
}

/// <summary>Part B: the same contract, run against the real disk store. Phase B4.</summary>
public sealed class LocalDiskFileStoreTests : FileStoreContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}
