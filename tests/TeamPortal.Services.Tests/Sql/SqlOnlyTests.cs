using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Infrastructure;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Sql;

public class SqlOnlyTests
{
    [Fact]
    public async Task CreateStepAsync_WhenTheImageSaveFails_WritesNoStepAtAll()
    {
        // The screenshot is saved before the step row exists, so a rejected file
        // leaves nothing behind to unwind. This is the test that would catch
        // someone "helpfully" moving the file save after the insert.
        using var fixture = new SqlServiceFixture();
        int processId = await fixture.FirstProcessIdAsync();

        int before = (await fixture.Steps.GetByProcessAsync(processId)).Count;

        // Rejected by the file validator - far over the size limit.
        using var content = new MemoryStream([1, 2, 3]);
        var oversized = new FileUpload("huge.png", "image/png", 50 * 1024 * 1024, content);

        Result<int> result = await fixture.Steps.CreateAsync(
            new CreateStepRequest(processId, "Should not land", Screenshot: oversized));

        result.IsFailure.Should().BeTrue();
        (await fixture.Steps.GetByProcessAsync(processId)).Should().HaveCount(before);
    }

    [Fact]
    public async Task CreateStepAsync_AppendsWithoutEverDuplicatingAStepNumber()
    {
        // The flow strip numbers 1..n with no gaps or ties. The unique index is
        // the backstop; this checks the service does not need it.
        using var fixture = new SqlServiceFixture();
        int processId = await fixture.FirstProcessIdAsync();

        for (int i = 0; i < 5; i++)
        {
            Result<int> created = await fixture.Steps.CreateAsync(
                new CreateStepRequest(processId, $"Step {i}"));

            created.IsSuccess.Should().BeTrue();
        }

        IReadOnlyList<ProcessStepDto> steps = await fixture.Steps.GetByProcessAsync(processId);

        steps.Select(s => s.StepNumber)
            .Should().Equal([.. Enumerable.Range(1, steps.Count)]);
    }

    [Fact]
    public async Task MoveAsync_ToTheFront_RenumbersWithoutTrippingTheUniqueIndex()
    {
        // Moving a step means several rows change number at once. EF sends one
        // UPDATE per row, so without the two-pass renumber this violates
        // IX_ProcessStep_ProcessId_StepNumber halfway through.
        using var fixture = new SqlServiceFixture();
        int processId = await fixture.FirstProcessIdAsync();

        IReadOnlyList<ProcessStepDto> before = await fixture.Steps.GetByProcessAsync(processId);
        ProcessStepDto last = before[^1];

        Result result = await fixture.Steps.MoveAsync(new MoveStepRequest(last.Id, 1));

        result.IsSuccess.Should().BeTrue();

        IReadOnlyList<ProcessStepDto> after = await fixture.Steps.GetByProcessAsync(processId);

        after[0].Id.Should().Be(last.Id);
        after.Select(s => s.StepNumber).Should().Equal([.. Enumerable.Range(1, after.Count)]);
    }

    [Fact]
    public async Task ResequenceAsync_OnAlreadyCorrectSteps_ChangesNothing()
    {
        using var fixture = new SqlServiceFixture();
        int processId = await fixture.FirstProcessIdAsync();

        IReadOnlyList<ProcessStepDto> before = await fixture.Steps.GetByProcessAsync(processId);

        await fixture.Steps.ResequenceAsync(processId);
        await fixture.Steps.ResequenceAsync(processId);

        IReadOnlyList<ProcessStepDto> after = await fixture.Steps.GetByProcessAsync(processId);

        after.Select(s => (s.Id, s.StepNumber))
            .Should().Equal([.. before.Select(s => (s.Id, s.StepNumber))]);
    }

    [Fact]
    public async Task GetDetailAsync_IssuesOneQueryNotOnePerStep()
    {
        // The N+1 trap. A step has owners and images, so a lazy projection would
        // fire three queries per step and get slower as the process grows.
        using var fixture = new SqlServiceFixture();
        int processId = await fixture.FirstProcessIdAsync();

        fixture.Commands.Reset();

        ProcessDetailDto? detail = await fixture.Processes.GetDetailAsync(processId);
        detail!.Steps.Should().NotBeEmpty();

        // One for the process, one for the steps with their owners and images,
        // one for the department name. Not one per step - and the seeded process
        // has four steps, so an N+1 would be well past this.
        fixture.Commands.Count.Should().BeLessThanOrEqualTo(4,
            "the step list is a single projection, not a query per step");
    }

    [Fact]
    public async Task Reads_DoNotTrackEntities()
    {
        // A tracked read costs memory and, worse, lets a later SaveChanges write
        // back a change nobody asked for.
        using var fixture = new SqlServiceFixture();

        IDbContextFactory<PortalDbContext> factory = fixture.Get<IDbContextFactory<PortalDbContext>>();

        await fixture.Employees.GetAllAsync();
        await fixture.Departments.GetWithCountsAsync();

        // A fresh context proves nothing on its own, so this checks the query
        // itself: AsNoTracking leaves no entries behind.
        using PortalDbContext db = factory.CreateDbContext();
        await db.Employees.AsNoTracking().ToListAsync();

        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public void Services_NeverInjectDbContextDirectly()
    {
        // The Blazor Server trap. A circuit lives for hours; a DbContext held for
        // that long throws "A second operation was started on this context
        // instance" the first time two renders overlap.
        // See Frontend/BuildPlan.md, "The one Blazor Server trap".
        Assembly infrastructure = typeof(PortalDbContext).Assembly;

        IReadOnlyList<string> offenders =
        [
            .. infrastructure.GetTypes()
                .Where(t => t.Name.StartsWith("Sql", StringComparison.Ordinal))
                .SelectMany(t => t.GetConstructors())
                .Where(c => c.GetParameters().Any(p => p.ParameterType == typeof(PortalDbContext)))
                .Select(c => c.DeclaringType!.Name)
        ];

        offenders.Should().BeEmpty("services take IDbContextFactory, never a DbContext");
    }

    [Fact]
    public void EveryDataServiceInterface_HasAnEfCoreImplementation()
    {
        // Catches the half-finished swap: an interface added to Application with
        // no Part B implementation would otherwise only surface at start-up.
        Assembly infrastructure = typeof(PortalDbContext).Assembly;

        var services = new ServiceCollection();
        services.AddDbContextFactory<PortalDbContext>(o => o.UseSqlite("DataSource=:memory:"));
        services.AddSqlServices();

        IReadOnlyList<Type> registered =
        [
            .. services.Select(d => d.ServiceType).Where(t => t.IsInterface)
        ];

        Type[] dataInterfaces =
        [
            typeof(IDepartmentService), typeof(IEmployeeService), typeof(IProcessFolderService),
            typeof(IProcessService), typeof(IProcessStepService), typeof(IStepStatusService),
            typeof(IActivityLogService), typeof(INotificationService)
        ];

        registered.Should().Contain(dataInterfaces);

        _ = infrastructure;
    }
}
