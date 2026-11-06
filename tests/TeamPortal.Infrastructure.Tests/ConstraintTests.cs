using Microsoft.EntityFrameworkCore;
using TeamPortal.Domain.Entities;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Tests.Common;

namespace TeamPortal.Infrastructure.Tests;

public class ConstraintTests
{
    /// <summary>Seeds the minimum needed to hang a process and step off.</summary>
    private static async Task<(int FolderId, int DepartmentId, int ProcessId)> ArrangeAsync(
        DatabaseFixture fixture)
    {
        await using PortalDbContext db = fixture.NewContext();

        db.StepStatuses.Add(new StepStatus
        {
            Id = 1,
            Name = "Not Started",
            ColourHex = "#7F7F85",
            IsDefault = true
        });

        var folder = new ProcessFolder { Name = "Client Onboarding", SortOrder = 1 };
        var department = new Department { Name = "Sales", ColourHex = "#E55218", SortOrder = 1 };

        db.ProcessFolders.Add(folder);
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        var process = new Process
        {
            Name = "Initial Contact",
            ProcessFolderId = folder.Id,
            DepartmentId = department.Id,
            StepStatusId = 1,
            SortOrder = 1
        };

        db.Processes.Add(process);
        await db.SaveChangesAsync();

        return (folder.Id, department.Id, process.Id);
    }

    [Fact]
    public async Task ProcessStep_TwoStepsWithTheSameNumberInOneProcess_IsRejected()
    {
        // The flow strip renumbers 1..n with no gaps or ties. This index is what
        // makes a bug in that renumbering fail loudly instead of corrupting order.
        await using var fixture = new DatabaseFixture();
        (_, _, int processId) = await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();
        db.ProcessSteps.Add(new ProcessStep { ProcessId = processId, StepNumber = 1, Name = "First", StepStatusId = 1 });
        await db.SaveChangesAsync();

        db.ProcessSteps.Add(new ProcessStep { ProcessId = processId, StepNumber = 1, Name = "Clash", StepStatusId = 1 });

        Func<Task> act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ProcessStep_TheSameNumberInADifferentProcess_IsAllowed()
    {
        // Uniqueness is per process, not global.
        await using var fixture = new DatabaseFixture();
        (int folderId, int departmentId, int processId) = await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();

        var second = new Process
        {
            Name = "Gather Info",
            ProcessFolderId = folderId,
            DepartmentId = departmentId,
            StepStatusId = 1,
            SortOrder = 2
        };

        db.Processes.Add(second);
        await db.SaveChangesAsync();

        db.ProcessSteps.Add(new ProcessStep { ProcessId = processId, StepNumber = 1, Name = "A", StepStatusId = 1 });
        db.ProcessSteps.Add(new ProcessStep { ProcessId = second.Id, StepNumber = 1, Name = "B", StepStatusId = 1 });

        Func<Task> act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Employee_ADuplicateEmail_IsRejected()
    {
        await using var fixture = new DatabaseFixture();
        (_, int departmentId, _) = await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();
        db.Employees.Add(new Employee { FullName = "Sarah", Email = "s@company.com", DepartmentId = departmentId });
        await db.SaveChangesAsync();

        db.Employees.Add(new Employee { FullName = "Someone Else", Email = "s@company.com", DepartmentId = departmentId });

        Func<Task> act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Employee_TwoEmployeesWithNoEmail_IsAllowed()
    {
        // The unique index is filtered on NULL - email is optional, so two
        // people without one must not collide.
        await using var fixture = new DatabaseFixture();
        (_, int departmentId, _) = await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();
        db.Employees.Add(new Employee { FullName = "Alex", DepartmentId = departmentId });
        db.Employees.Add(new Employee { FullName = "Priya", DepartmentId = departmentId });

        Func<Task> act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Department_ADuplicateName_IsRejected()
    {
        await using var fixture = new DatabaseFixture();
        await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();
        db.Departments.Add(new Department { Name = "Sales", ColourHex = "#E55218" });

        Func<Task> act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Process_TwoProcessesAtTheSamePositionInAFolder_IsRejected()
    {
        // SortOrder is the flow-strip position, so a tie would make the order
        // arbitrary and Back/Next non-deterministic.
        await using var fixture = new DatabaseFixture();
        (int folderId, int departmentId, _) = await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();

        db.Processes.Add(new Process
        {
            Name = "Gather Info",
            ProcessFolderId = folderId,
            DepartmentId = departmentId,
            StepStatusId = 1,
            SortOrder = 1
        });

        Func<Task> act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Department_WithEmployeesInIt_CannotBeHardDeleted()
    {
        // Restrict. Move the staff first - see Frontend/DataModel.md.
        await using var fixture = new DatabaseFixture();
        (_, int departmentId, _) = await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();
        db.Employees.Add(new Employee { FullName = "Sarah", DepartmentId = departmentId });
        await db.SaveChangesAsync();

        // ExecuteDelete bypasses the soft-delete override, so this exercises the
        // database's own referential action rather than our SaveChanges hook.
        Func<Task> act = async () =>
            await db.Departments.Where(d => d.Id == departmentId).ExecuteDeleteAsync();

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Process_WhenHardDeleted_TakesItsStepsWithIt()
    {
        // Cascade. A step has no meaning without its process.
        await using var fixture = new DatabaseFixture();
        (_, _, int processId) = await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();
        db.ProcessSteps.Add(new ProcessStep { ProcessId = processId, StepNumber = 1, Name = "First", StepStatusId = 1 });
        await db.SaveChangesAsync();

        await db.Processes.Where(p => p.Id == processId).ExecuteDeleteAsync();

        await using PortalDbContext verify = fixture.NewContext();
        (await verify.ProcessSteps.CountAsync(s => s.ProcessId == processId)).Should().Be(0);
    }

    [Fact]
    public async Task StepOwner_TheSamePersonTwiceOnOneStep_IsRejected()
    {
        // The composite primary key is what enforces this.
        await using var fixture = new DatabaseFixture();
        (_, int departmentId, int processId) = await ArrangeAsync(fixture);

        await using PortalDbContext db = fixture.NewContext();

        var employee = new Employee { FullName = "Sarah", DepartmentId = departmentId };
        var step = new ProcessStep { ProcessId = processId, StepNumber = 1, Name = "First", StepStatusId = 1 };
        db.Employees.Add(employee);
        db.ProcessSteps.Add(step);
        await db.SaveChangesAsync();

        db.StepOwners.Add(new StepOwner { ProcessStepId = step.Id, EmployeeId = employee.Id });
        await db.SaveChangesAsync();

        await using PortalDbContext second = fixture.NewContext();
        second.StepOwners.Add(new StepOwner { ProcessStepId = step.Id, EmployeeId = employee.Id });

        Func<Task> act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
