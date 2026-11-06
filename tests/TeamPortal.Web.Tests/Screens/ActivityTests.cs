using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;
using TeamPortal.Web.Components.Layout;
using TeamPortal.Web.Components.Shared;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public class ActivityListTests : ComponentTestBase
{
    private IActivityLogService Log => Services.GetRequiredService<IActivityLogService>();

    private IRenderedComponent<ActivityList> RenderList(
        string? entityName = null, int? entityId = null, int pageSize = 25)
    {
        IRenderedComponent<ActivityList> cut = Render<ActivityList>(p => p
            .Add(x => x.EntityName, entityName)
            .Add(x => x.EntityId, entityId)
            .Add(x => x.PageSize, pageSize));

        cut.WaitForAssertion(() => cut.FindAll(".app-loading").Should().BeEmpty());
        return cut;
    }

    [Fact]
    public void ActivityList_WithNoHistory_ShowsTheEmptyState()
    {
        IRenderedComponent<ActivityList> cut = RenderList();

        cut.Find(".app-empty-state__title").TextContent
            .Should().Contain("Nothing has happened here yet");
    }

    [Fact]
    public async Task ActivityList_AfterAWrite_ShowsTheEntry()
    {
        await Services.GetRequiredService<IEmployeeService>()
            .CreateAsync(new CreateEmployeeRequest("Nomsa", 6));

        IRenderedComponent<ActivityList> cut = RenderList();

        cut.FindAll(".app-activity__item").Should().NotBeEmpty();
        cut.Markup.Should().Contain("Added Nomsa");
    }

    [Fact]
    public async Task ActivityList_ShowsNewestFirst()
    {
        IEmployeeService employees = Services.GetRequiredService<IEmployeeService>();
        await employees.CreateAsync(new CreateEmployeeRequest("First", 6));
        await employees.CreateAsync(new CreateEmployeeRequest("Second", 6));

        IRenderedComponent<ActivityList> cut = RenderList();

        cut.FindAll(".app-activity__summary")[0].TextContent.Should().Contain("Second");
    }

    [Fact]
    public async Task ActivityList_ScopedToAnEntity_ShowsOnlyItsHistory()
    {
        IEmployeeService employees = Services.GetRequiredService<IEmployeeService>();
        var created = await employees.CreateAsync(new CreateEmployeeRequest("Nomsa", 6));
        await employees.CreateAsync(new CreateEmployeeRequest("Someone Else", 6));

        IRenderedComponent<ActivityList> cut = RenderList("Employee", created.Value);

        cut.FindAll(".app-activity__item").Should().ContainSingle();
        cut.Markup.Should().Contain("Nomsa").And.NotContain("Someone Else");
    }

    [Fact]
    public async Task ActivityList_ShowsWhoDidItAndWhen()
    {
        await Services.GetRequiredService<IAuthService>()
            .SignInAsync(new SignInRequest("admin@company.com", "Portal!2026"));

        IRenderedComponent<ActivityList> cut = RenderList();

        cut.Find(".app-activity__meta").TextContent.Should().Contain("Admin User");
    }

    [Fact]
    public async Task ActivityList_WithOnePage_HidesThePager()
    {
        await Services.GetRequiredService<IEmployeeService>()
            .CreateAsync(new CreateEmployeeRequest("Nomsa", 6));

        IRenderedComponent<ActivityList> cut = RenderList();

        cut.FindAll(".app-activity__pager").Should().BeEmpty();
    }

    [Fact]
    public async Task ActivityList_WithMoreThanAPage_ShowsThePager()
    {
        // The log only ever grows, so an unpaged read gets slower every day.
        IEmployeeService employees = Services.GetRequiredService<IEmployeeService>();

        for (int i = 0; i < 5; i++)
        {
            await employees.CreateAsync(new CreateEmployeeRequest($"Person {i}", 6));
        }

        IRenderedComponent<ActivityList> cut = RenderList(pageSize: 2);

        cut.Find(".app-activity__pager").Should().NotBeNull();
        cut.Find(".app-activity__pagecount").TextContent.Should().Contain("Page 1 of 3");
        cut.FindAll(".app-activity__item").Should().HaveCount(2);
    }

    [Fact]
    public async Task ActivityList_OnNext_MovesToTheSecondPage()
    {
        IEmployeeService employees = Services.GetRequiredService<IEmployeeService>();

        for (int i = 0; i < 5; i++)
        {
            await employees.CreateAsync(new CreateEmployeeRequest($"Person {i}", 6));
        }

        IRenderedComponent<ActivityList> cut = RenderList(pageSize: 2);
        await cut.FindAll(".app-activity__pager button")[^1].ClickAsync(new());

        cut.WaitForAssertion(() =>
            cut.Find(".app-activity__pagecount").TextContent.Should().Contain("Page 2 of 3"));
    }

    [Fact]
    public async Task ActivityList_OnTheFirstPage_PreviousIsDisabled()
    {
        IEmployeeService employees = Services.GetRequiredService<IEmployeeService>();

        for (int i = 0; i < 5; i++)
        {
            await employees.CreateAsync(new CreateEmployeeRequest($"Person {i}", 6));
        }

        IRenderedComponent<ActivityList> cut = RenderList(pageSize: 2);

        cut.FindAll(".app-activity__pager button")[0].HasAttribute("disabled").Should().BeTrue();
    }
}

public class NotificationBellTests : ComponentTestBase
{
    [Fact]
    public void NotificationBell_WithNothingUnread_ShowsNoBadge()
    {
        IRenderedComponent<NotificationBell> cut = Render<NotificationBell>();

        cut.WaitForAssertion(() =>
            cut.Find("button").GetAttribute("aria-label").Should().Contain("none unread"));
    }

    [Fact]
    public async Task NotificationBell_WithUnread_CountsThemInItsLabel()
    {
        // Seeded through the service so the test does not reach into the store.
        INotificationService notifications = Services.GetRequiredService<INotificationService>();
        await Services.GetRequiredService<IAuthService>()
            .SignInAsync(new SignInRequest("admin@company.com", "Portal!2026"));

        int unread = await notifications.GetUnreadCountAsync();

        IRenderedComponent<NotificationBell> cut = Render<NotificationBell>();

        cut.WaitForAssertion(() =>
            cut.Find("button").GetAttribute("aria-label")
                .Should().Contain(unread == 0 ? "none unread" : $"{unread} unread"));
    }

    [Fact]
    public async Task NotificationService_MarkAllAsRead_ClearsTheCount()
    {
        await Services.GetRequiredService<IAuthService>()
            .SignInAsync(new SignInRequest("admin@company.com", "Portal!2026"));

        INotificationService notifications = Services.GetRequiredService<INotificationService>();
        await notifications.MarkAllAsReadAsync();

        (await notifications.GetUnreadCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NotificationService_WhenSignedOut_ReturnsNothing()
    {
        INotificationService notifications = Services.GetRequiredService<INotificationService>();

        IReadOnlyList<NotificationDto> mine = await notifications.GetForCurrentUserAsync();

        mine.Should().BeEmpty("notifications belong to a user, and nobody is signed in");
    }
}

public class ActivityCoverageTests : ComponentTestBase
{
    /// <summary>
    /// Every write path must leave a trace, or the log quietly under-reports.
    /// This is the phase where those gaps show up.
    /// </summary>
    [Theory]
    [InlineData("Employee")]
    [InlineData("Department")]
    [InlineData("Process")]
    [InlineData("ProcessStep")]
    public async Task EveryEntityType_RecordsACreateInTheLog(string entityName)
    {
        int id = await CreateOneAsync(entityName);

        PagedResult<ActivityLogEntryDto> log = await Services
            .GetRequiredService<IActivityLogService>()
            .GetForEntityAsync(entityName, id);

        log.Items.Should().ContainSingle()
            .Which.Action.Should().Be(ActivityAction.Created);
    }

    private async Task<int> CreateOneAsync(string entityName) => entityName switch
    {
        "Employee" => (await Services.GetRequiredService<IEmployeeService>()
            .CreateAsync(new CreateEmployeeRequest("Nomsa", 6))).Value,

        "Department" => (await Services.GetRequiredService<IDepartmentService>()
            .CreateAsync(new CreateDepartmentRequest("New Department", "#4A2767"))).Value,

        "Process" => (await Services.GetRequiredService<IProcessService>()
            .CreateAsync(new CreateProcessRequest("Renewals Process", 2))).Value,

        "ProcessStep" => (await Services.GetRequiredService<IProcessStepService>()
            .CreateAsync(new CreateStepRequest(1, "Final Review"))).Value,

        _ => throw new ArgumentOutOfRangeException(nameof(entityName))
    };
}
