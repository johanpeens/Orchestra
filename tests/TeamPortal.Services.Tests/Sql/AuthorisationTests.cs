using System.Reflection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Sql;

public class AuthorisationTests
{
    // --- Reads are open to anyone signed in ----------------------------------

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.Manager)]
    [InlineData(AppRoles.Member)]
    [InlineData(AppRoles.Viewer)]
    public async Task Reads_AreAllowedForEveryRole(string role)
    {
        using var fixture = new SqlServiceFixture();
        fixture.SignInAs(1, role);

        (await fixture.Employees.GetAllAsync()).Should().NotBeEmpty();
        (await fixture.Departments.GetWithCountsAsync()).Should().NotBeEmpty();
    }

    // --- Writes are not -------------------------------------------------------

    [Theory]
    [InlineData(AppRoles.Member)]
    [InlineData(AppRoles.Viewer)]
    public async Task CreateEmployee_ForSomeoneWhoMayNotEdit_IsRefused(string role)
    {
        using var fixture = new SqlServiceFixture();
        int departmentId = await fixture.EnablementDepartmentIdAsync();
        fixture.SignInAs(1, role);

        Result<int> result = await fixture.Employees.CreateAsync(
            new CreateEmployeeRequest("Nomsa", departmentId));

        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(AppRoles.Member)]
    [InlineData(AppRoles.Viewer)]
    public async Task CreateEmployee_ForSomeoneWhoMayNotEdit_WritesNothing(string role)
    {
        // A refusal that still wrote the row would be worse than no check at all.
        using var fixture = new SqlServiceFixture();
        int departmentId = await fixture.EnablementDepartmentIdAsync();
        int before = (await fixture.Employees.GetAllAsync()).Count;

        fixture.SignInAs(1, role);
        await fixture.Employees.CreateAsync(new CreateEmployeeRequest("Nomsa", departmentId));

        fixture.SignInAs(1, AppRoles.Admin);
        (await fixture.Employees.GetAllAsync()).Should().HaveCount(before);
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.Manager)]
    public async Task CreateEmployee_ForAnEditor_IsAllowed(string role)
    {
        using var fixture = new SqlServiceFixture();
        int departmentId = await fixture.EnablementDepartmentIdAsync();
        fixture.SignInAs(1, role);

        Result<int> result = await fixture.Employees.CreateAsync(
            new CreateEmployeeRequest("Nomsa", departmentId));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Writes_ForSomeoneNotSignedInAtAll_AreRefused()
    {
        // A null user is in no role, so every IsInRole check says false. This
        // test exists because "in no role" and "not signed in" are different
        // bugs, and only one of them is obvious.
        using var fixture = new SqlServiceFixture();
        int departmentId = await fixture.EnablementDepartmentIdAsync();

        fixture.SignOut();

        Result<int> employee = await fixture.Employees.CreateAsync(
            new CreateEmployeeRequest("Nomsa", departmentId));

        Result<int> department = await fixture.Departments.CreateAsync(
            new CreateDepartmentRequest("Ghost Unit", BrandPalette.Aqua100));

        employee.IsFailure.Should().BeTrue();
        department.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDepartment_ForAViewer_IsRefused()
    {
        using var fixture = new SqlServiceFixture();
        IReadOnlyList<DepartmentListItemDto> departments =
            await fixture.Departments.GetWithCountsAsync();
        int empty = departments.First(d => d.EmployeeCount == 0).Id;

        fixture.SignInAs(1, AppRoles.Viewer);

        Result result = await fixture.Departments.DeleteAsync(empty);

        result.IsFailure.Should().BeTrue();
    }

    // --- Uploading is a lower bar than editing -------------------------------

    [Fact]
    public async Task CreateStep_ForAMember_IsAllowed()
    {
        // Members write up steps and attach screenshots. That is the job.
        using var fixture = new SqlServiceFixture();
        int processId = await fixture.FirstProcessIdAsync();

        fixture.SignInAs(1, AppRoles.Member);

        Result<int> result = await fixture.Steps.CreateAsync(
            new CreateStepRequest(processId, "Written up by a member"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateStep_ForAViewer_IsRefused()
    {
        using var fixture = new SqlServiceFixture();
        int processId = await fixture.FirstProcessIdAsync();

        fixture.SignInAs(1, AppRoles.Viewer);

        Result<int> result = await fixture.Steps.CreateAsync(
            new CreateStepRequest(processId, "Should not land"));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task MoveStep_ForAMember_IsRefused()
    {
        // Adding a step is a Member's job. Reordering the flow is not.
        using var fixture = new SqlServiceFixture();
        int processId = await fixture.FirstProcessIdAsync();
        IReadOnlyList<ProcessStepDto> steps = await fixture.Steps.GetByProcessAsync(processId);

        fixture.SignInAs(1, AppRoles.Member);

        Result result = await fixture.Steps.MoveAsync(new MoveStepRequest(steps[^1].Id, 1));

        result.IsFailure.Should().BeTrue();
    }

    // --- The message ----------------------------------------------------------

    [Fact]
    public async Task ARefusal_DoesNotSayWhichRoleWouldHaveWorked()
    {
        // Which roles exist and what each may do is not the caller's business.
        using var fixture = new SqlServiceFixture();
        int departmentId = await fixture.EnablementDepartmentIdAsync();

        fixture.SignInAs(1, AppRoles.Viewer);

        Result<int> result = await fixture.Employees.CreateAsync(
            new CreateEmployeeRequest("Nomsa", departmentId));

        foreach (string role in AppRoles.All)
        {
            result.ErrorSummary.Should().NotContain(role);
        }
    }

    [Fact]
    public async Task ARefusal_IsAFormLevelError()
    {
        // It belongs to the form as a whole, not under one field - there is no
        // field the user could change to make it succeed.
        using var fixture = new SqlServiceFixture();
        int departmentId = await fixture.EnablementDepartmentIdAsync();

        fixture.SignInAs(1, AppRoles.Viewer);

        Result<int> result = await fixture.Employees.CreateAsync(
            new CreateEmployeeRequest("Nomsa", departmentId));

        result.Errors.Should().ContainSingle()
            .Which.Field.Should().Be(ValidationError.FormLevel);
    }

    // --- Sweeps ---------------------------------------------------------------

    [Fact]
    public void EveryWriteMethod_OnEveryEfCoreService_AsksTheGuard()
    {
        // A reflection sweep would only see the call if it read IL, so this reads
        // the source instead: crude, but it covers a service written next year,
        // and the failure message points at the file.
        string servicesFolder = FindServicesFolder();

        var offenders = new List<string>();

        foreach (string file in Directory.GetFiles(servicesFolder, "Sql*.cs"))
        {
            string text = File.ReadAllText(file);

            foreach (string method in new[] { "CreateAsync", "UpdateAsync", "DeleteAsync", "MoveAsync" })
            {
                int at = text.IndexOf($"public async Task<Result> {method}", StringComparison.Ordinal);
                int atGeneric = text.IndexOf($"public async Task<Result<int>> {method}", StringComparison.Ordinal);
                int start = at >= 0 ? at : atGeneric;

                if (start < 0)
                {
                    continue;
                }

                // The guard has to be inside the first few lines of the body,
                // before anything is read or written.
                string opening = text[start..Math.Min(text.Length, start + 900)];

                if (!opening.Contains("AuthorisationGuard", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}.{method}");
                }
            }
        }

        offenders.Should().BeEmpty("every write path checks the caller's role before doing anything");
    }

    [Fact]
    public void EveryServiceMethod_AcceptsACancellationToken()
    {
        // An abandoned circuit must be able to stop work it started.
        Assembly application = typeof(ICurrentUserService).Assembly;

        IReadOnlyList<string> missing =
        [
            .. application.GetTypes()
                .Where(t => t.IsInterface && t.Name.EndsWith("Service", StringComparison.Ordinal))
                .SelectMany(t => t.GetMethods())
                .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
                .Where(m => !m.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
        ];

        missing.Should().BeEmpty();
    }

    private static string FindServicesFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the test has to be able to find the repository root");

        return Path.Combine(
            directory!.FullName, "src", "TeamPortal.Infrastructure", "Services");
    }
}
