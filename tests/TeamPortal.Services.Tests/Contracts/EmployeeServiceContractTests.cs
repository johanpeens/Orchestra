using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Contracts;

public abstract class EmployeeServiceContractTests : IDisposable
{
    protected EmployeeServiceContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    /// <summary>Part B overrides this to return a SQLite-backed graph.</summary>
    protected abstract ServiceFixture CreateFixture();

    private IEmployeeService Sut => Fixture.Employees;

    // --- Reads ---------------------------------------------------------------

    [Fact]
    public async Task GetByDepartmentAsync_ReturnsOnlyThatDepartment()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        IReadOnlyList<EmployeeCardDto> cards = await Sut.GetByDepartmentAsync(enablement);

        cards.Should().NotBeEmpty();
        cards.Should().OnlyContain(c => c.DepartmentName == "Business Enablement");
    }

    [Fact]
    public async Task GetByDepartmentAsync_ForAnEmptyDepartment_ReturnsEmptyNotNull()
    {
        IReadOnlyList<DepartmentListItemDto> departments = await Fixture.Departments.GetWithCountsAsync();
        int empty = departments.First(d => d.Name == "Underwriting").Id;

        IReadOnlyList<EmployeeCardDto> cards = await Sut.GetByDepartmentAsync(empty);

        cards.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetByDepartmentAsync_OrdersByFullName()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        IReadOnlyList<EmployeeCardDto> cards = await Sut.GetByDepartmentAsync(enablement);

        cards.Select(c => c.FullName)
            .Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetByDepartmentAsync_ExcludesInactiveEmployees()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();
        IReadOnlyList<EmployeeCardDto> before = await Sut.GetByDepartmentAsync(enablement);
        EmployeeCardDto target = before[0];

        EmployeeDetailDto detail = (await Sut.GetByIdAsync(target.Id))!;
        await Sut.UpdateAsync(new UpdateEmployeeRequest(
            detail.Id, detail.FullName, detail.DepartmentId,
            detail.JobTitle, detail.Email, detail.StaffType, IsActive: false));

        IReadOnlyList<EmployeeCardDto> after = await Sut.GetByDepartmentAsync(enablement);

        after.Should().HaveCount(before.Count - 1);
        after.Should().NotContain(c => c.Id == target.Id);
    }

    [Fact]
    public async Task GetByDepartmentAsync_ExcludesDeletedEmployees()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();
        IReadOnlyList<EmployeeCardDto> before = await Sut.GetByDepartmentAsync(enablement);

        // Someone with no step assignments, so the Restrict rule does not block.
        EmployeeCardDto target = before.First(c => c.FullName == "Alex");
        Result deleted = await Sut.DeleteAsync(target.Id);
        deleted.IsSuccess.Should().BeTrue();

        IReadOnlyList<EmployeeCardDto> after = await Sut.GetByDepartmentAsync(enablement);

        after.Should().NotContain(c => c.Id == target.Id);
    }

    [Fact]
    public async Task SearchAsync_IsCaseInsensitive()
    {
        IReadOnlyList<EmployeeCardDto> lower = await Sut.SearchAsync("sarah");
        IReadOnlyList<EmployeeCardDto> upper = await Sut.SearchAsync("SARAH");

        lower.Should().NotBeEmpty();
        upper.Select(c => c.Id).Should().BeEquivalentTo(lower.Select(c => c.Id));
    }

    [Fact]
    public async Task SearchAsync_MatchesOnJobTitle()
    {
        IReadOnlyList<EmployeeCardDto> results = await Sut.SearchAsync("Business Analyst");

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(c => c.JobTitle == "Business Analyst");
    }

    [Fact]
    public async Task SearchAsync_MatchesOnDepartmentName()
    {
        IReadOnlyList<EmployeeCardDto> results = await Sut.SearchAsync("Enablement");

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithABlankTerm_ReturnsEveryone()
    {
        IReadOnlyList<EmployeeCardDto> all = await Sut.GetAllAsync();
        IReadOnlyList<EmployeeCardDto> blank = await Sut.SearchAsync("   ");

        blank.Should().HaveCount(all.Count);
    }

    [Fact]
    public async Task SearchAsync_WithNoMatch_ReturnsEmpty()
    {
        IReadOnlyList<EmployeeCardDto> results = await Sut.SearchAsync("zzzzzzz");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOptionsAsync_ReturnsActiveEmployeesOrderedByName()
    {
        IReadOnlyList<EmployeeOptionDto> options = await Sut.GetOptionsAsync();

        options.Should().NotBeEmpty();
        options.Select(o => o.FullName)
            .Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetByIdAsync_ForAnUnknownId_ReturnsNull()
    {
        (await Sut.GetByIdAsync(999_999)).Should().BeNull();
    }

    // --- Copy semantics ------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_MutatingTheResult_DoesNotChangeTheStore()
    {
        // A DTO handed back by reference would let a bound form field edit "the
        // database" directly - and it would only break after the Part B swap,
        // because EF Core always hands back a detached copy.
        EmployeeDetailDto first = (await Sut.GetByIdAsync(1))!;
        _ = first with { FullName = "Tampered" };

        EmployeeDetailDto second = (await Sut.GetByIdAsync(1))!;

        second.FullName.Should().NotBe("Tampered");
        second.FullName.Should().Be(first.FullName);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsADistinctListEachCall()
    {
        IReadOnlyList<EmployeeCardDto> a = await Sut.GetAllAsync();
        IReadOnlyList<EmployeeCardDto> b = await Sut.GetAllAsync();

        a.Should().NotBeSameAs(b);
    }

    // --- Writes --------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WithAValidRequest_InsertsAndReturnsTheNewId()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result<int> result = await Sut.CreateAsync(
            new CreateEmployeeRequest("Nomsa", enablement, "Data Engineer"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);

        EmployeeDetailDto? created = await Sut.GetByIdAsync(result.Value);
        created.Should().NotBeNull();
        created!.FullName.Should().Be("Nomsa");
    }

    [Fact]
    public async Task CreateAsync_TrimsWhitespaceFromTheName()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result<int> result = await Sut.CreateAsync(
            new CreateEmployeeRequest("   Nomsa   ", enablement));

        EmployeeDetailDto created = (await Sut.GetByIdAsync(result.Value))!;
        created.FullName.Should().Be("Nomsa");
    }

    [Fact]
    public async Task CreateAsync_AssignsSequentialIds()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result<int> first = await Sut.CreateAsync(new CreateEmployeeRequest("One", enablement));
        Result<int> second = await Sut.CreateAsync(new CreateEmployeeRequest("Two", enablement));

        second.Value.Should().Be(first.Value + 1, "ids must behave like IDENTITY(1,1)");
    }

    [Fact]
    public async Task CreateAsync_WithABlankName_ReturnsValidationFailure()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result<int> result = await Sut.CreateAsync(new CreateEmployeeRequest("  ", enablement));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == nameof(CreateEmployeeRequest.FullName));
    }

    [Fact]
    public async Task CreateAsync_WithAnUnknownDepartment_ReturnsValidationFailure()
    {
        Result<int> result = await Sut.CreateAsync(new CreateEmployeeRequest("Nomsa", 999_999));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == nameof(CreateEmployeeRequest.DepartmentId));
    }

    [Fact]
    public async Task CreateAsync_WithADuplicateEmail_ReturnsValidationFailure()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result<int> result = await Sut.CreateAsync(
            new CreateEmployeeRequest("Impostor", enablement, Email: "sarah@company.com"));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == nameof(CreateEmployeeRequest.Email));
    }

    [Fact]
    public async Task CreateAsync_WhenValidationFails_WritesNothing()
    {
        IReadOnlyList<EmployeeCardDto> before = await Sut.GetAllAsync();

        await Sut.CreateAsync(new CreateEmployeeRequest("", 0));

        IReadOnlyList<EmployeeCardDto> after = await Sut.GetAllAsync();
        after.Should().HaveCount(before.Count);
    }

    [Fact]
    public async Task CreateAsync_OnSuccess_WritesAnActivityLogRow()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result<int> created = await Sut.CreateAsync(new CreateEmployeeRequest("Nomsa", enablement));

        PagedResult<ActivityLogEntryDto> log =
            await Fixture.ActivityLog.GetForEntityAsync("Employee", created.Value);

        log.Items.Should().ContainSingle()
            .Which.Action.Should().Be(ActivityAction.Created);
    }

    [Fact]
    public async Task UpdateAsync_WithTheSameEmailAsItself_Succeeds()
    {
        // The classic uniqueness bug: a naive check compares against every row
        // including the one being edited, so saving without changing anything
        // fails with "already taken".
        EmployeeDetailDto sarah = (await Sut.GetByIdAsync(1))!;

        Result result = await Sut.UpdateAsync(new UpdateEmployeeRequest(
            sarah.Id, sarah.FullName, sarah.DepartmentId,
            sarah.JobTitle, sarah.Email, sarah.StaffType));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ForAnUnknownId_ReturnsNotFound()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();

        Result result = await Sut.UpdateAsync(new UpdateEmployeeRequest(999_999, "Ghost", enablement));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForSomeoneOwningAStep_IsRefused()
    {
        // Sarah owns step 1 of the seeded process. Restrict, per DataModel.md.
        Result result = await Sut.DeleteAsync(1);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Field.Should().Be(ValidationError.FormLevel);
    }

    [Fact]
    public async Task DeleteAsync_ForAnUnassignedPerson_Succeeds()
    {
        IReadOnlyList<EmployeeCardDto> all = await Sut.GetAllAsync();
        EmployeeCardDto alex = all.First(c => c.FullName == "Alex");

        Result result = await Sut.DeleteAsync(alex.Id);

        result.IsSuccess.Should().BeTrue();
        (await Sut.GetByIdAsync(alex.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ForAnUnknownId_ReturnsNotFound()
    {
        (await Sut.DeleteAsync(999_999)).IsFailure.Should().BeTrue();
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Part A: runs the contract against the in-memory implementation.</summary>
public sealed class MockEmployeeServiceTests : EmployeeServiceContractTests
{
    private MockServiceFixture Mock => (MockServiceFixture)Fixture;

    protected override ServiceFixture CreateFixture() => new MockServiceFixture();

    // --- Simulated failure ---------------------------------------------------
    // Not part of the contract. FailNextWrite is a mock-only switch that exists so
    // the Part A screens could be built against a failing save; EF Core has no
    // equivalent, and the transaction tests cover the same ground on that side.

    [Fact]
    public async Task CreateAsync_WhenTheWriteIsMadeToFail_ReturnsAFormLevelError()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();
        Mock.Options.FailNextWrite = true;

        Result<int> result = await Fixture.Employees.CreateAsync(
            new CreateEmployeeRequest("Nomsa", enablement));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Field.Should().Be(ValidationError.FormLevel);
    }

    [Fact]
    public async Task CreateAsync_WhenTheWriteIsMadeToFail_WritesNothing()
    {
        int enablement = await Fixture.EnablementDepartmentIdAsync();
        IReadOnlyList<EmployeeCardDto> before = await Fixture.Employees.GetAllAsync();
        Mock.Options.FailNextWrite = true;

        await Fixture.Employees.CreateAsync(new CreateEmployeeRequest("Nomsa", enablement));

        (await Fixture.Employees.GetAllAsync()).Should().HaveCount(before.Count);
    }
}

/// <summary>Part B: the same contract, run against EF Core over SQLite.</summary>
public sealed class SqlEmployeeServiceTests : EmployeeServiceContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}
