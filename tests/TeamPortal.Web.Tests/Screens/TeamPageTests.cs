using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Web.Components.Pages;
using TeamPortal.Web.Components.Shared;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public class TeamPageTests : ComponentTestBase
{
    private IRenderedComponent<TeamPage> RenderTeam()
    {
        IRenderedComponent<TeamPage> cut = Render<TeamPage>();
        cut.WaitForAssertion(() => cut.FindAll(".app-empcard").Should().NotBeEmpty());
        return cut;
    }

    private static IElement Search(IRenderedComponent<TeamPage> cut) =>
        cut.Find(".app-team__search input");

    // --- Header ---------------------------------------------------------------

    [Fact]
    public void TeamPage_ShowsTheTitleAndDescriptionFromTheDesign()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.Find(".app-page-header__title").TextContent.Should().Contain("Team");
        cut.Find(".app-page-header__description").TextContent
            .Should().Contain("Manage and view members, roles, and business enablement structures.");
    }

    [Fact]
    public void TeamPage_HasNoOverline()
    {
        // docs/design/05 has a description line where other pages have an overline.
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.FindAll(".app-page-header__overline").Should().BeEmpty();
    }

    [Fact]
    public void TeamPage_ShowsAnEditPencilOnEachEmployeeCard()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.FindAll(".app-empcard__edit").Should().HaveCount(8);
        cut.FindAll(".app-empcard__edit").Should()
            .OnlyContain(b => b.GetAttribute("aria-label")!.StartsWith("Edit ", StringComparison.Ordinal));
    }

    [Fact]
    public void TeamPage_HasAViewActivityLogButton()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.Find(".app-team__log").TextContent.Should().Contain("View Activity Log");
    }

    [Fact]
    public void TeamPage_HasASearchBoxAndAnAddEmployeeButton()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        Search(cut).GetAttribute("placeholder")
            .Should().Be("Search name, role or department...");
        cut.Find(".app-team__add").TextContent.Should().Contain("Add Employee");
    }

    // --- Cards ----------------------------------------------------------------

    [Fact]
    public void TeamPage_RendersACardPerSeededEmployee()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.FindAll(".app-empcard").Should().HaveCount(8);
    }

    [Fact]
    public void TeamPage_ShowsNameRoleAndDepartmentOnEachCard()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.Markup.Should().Contain("Sarah").And.Contain("Project Manager");
        cut.Markup.Should().Contain("Business Enablement");
    }

    [Fact]
    public void TeamPage_OrdersCardsByName()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.FindAll(".app-empcard__name").Select(e => e.TextContent.Trim())
            .Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    // --- Department filter ------------------------------------------------------

    [Fact]
    public void TeamPage_StartsOnTheAllFilter()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.Find(".app-deptfilter__row--selected").TextContent.Should().Contain("All");
    }

    [Fact]
    public void TeamPage_AllCountMatchesTheNumberOfCards()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.Find(".app-deptfilter__row--selected .app-deptfilter__count")
            .TextContent.Trim().Should().Be("(8)");
        cut.FindAll(".app-empcard").Should().HaveCount(8);
    }

    [Fact]
    public void TeamPage_ShowsACountPerDepartment()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        // Seven seeded departments, plus the "All" row.
        cut.FindAll(".app-deptfilter__row").Should().HaveCount(8);
    }

    [Fact]
    public void TeamPage_ShowsALabelledEditPencilPerDepartment()
    {
        // Bound from phase A6 onward. Seven seeded departments; the "All" row has
        // no pencil because there is no such department to edit.
        IRenderedComponent<TeamPage> cut = RenderTeam();

        cut.FindAll(".app-deptfilter__edit").Should().HaveCount(7);
        cut.FindAll(".app-deptfilter__edit").Should()
            .OnlyContain(b => b.GetAttribute("aria-label")!.StartsWith("Edit ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TeamPage_OnSelectingADepartment_ShowsOnlyThatDepartment()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        IElement enablement = cut.FindAll(".app-deptfilter__row")
            .First(r => r.TextContent.Contains("Business Enablement", StringComparison.Ordinal));
        await enablement.ClickAsync(new());

        cut.WaitForAssertion(() =>
            cut.FindAll(".app-empcard").Should().HaveCount(8));
        cut.FindAll(".app-empcard__dept").Should()
            .OnlyContain(e => e.TextContent.Contains("Business Enablement", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TeamPage_OnSelectingAnEmptyDepartment_ShowsTheEmptyState()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        IElement underwriting = cut.FindAll(".app-deptfilter__row")
            .First(r => r.TextContent.Contains("Underwriting", StringComparison.Ordinal));
        await underwriting.ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".app-empcard").Should().BeEmpty();
            cut.Find(".app-empty-state__title").TextContent
                .Should().Contain("No people in this department yet");
        });
    }

    [Fact]
    public async Task TeamPage_MarksTheSelectedDepartmentForScreenReaders()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        IElement sales = cut.FindAll(".app-deptfilter__row")
            .First(r => r.TextContent.Contains("Sales", StringComparison.Ordinal));
        await sales.ClickAsync(new());

        cut.WaitForAssertion(() =>
            cut.Find(".app-deptfilter__row--selected").GetAttribute("aria-pressed")
                .Should().Be("true"));
    }

    // --- Search -----------------------------------------------------------------

    [Fact]
    public async Task TeamPage_SearchingByName_FiltersTheCards()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        await Search(cut).InputAsync(new() { Value = "Sarah" });

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".app-empcard").Should().ContainSingle();
            cut.Find(".app-empcard__name").TextContent.Should().Contain("Sarah");
        });
    }

    [Fact]
    public async Task TeamPage_SearchingByRole_FiltersTheCards()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        await Search(cut).InputAsync(new() { Value = "Business Analyst" });

        cut.WaitForAssertion(() =>
            cut.FindAll(".app-empcard").Should().HaveCount(2));
    }

    [Fact]
    public async Task TeamPage_SearchIsCaseInsensitive()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        await Search(cut).InputAsync(new() { Value = "sARAh" });

        cut.WaitForAssertion(() => cut.FindAll(".app-empcard").Should().ContainSingle());
    }

    [Fact]
    public async Task TeamPage_SearchWithNoMatch_ShowsTheSearchEmptyState()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();

        await Search(cut).InputAsync(new() { Value = "zzzzzzz" });

        cut.WaitForAssertion(() =>
            cut.Find(".app-empty-state__title").TextContent
                .Should().Contain("No one matches that search"));
    }

    [Fact]
    public async Task TeamPage_SearchingResetsTheDepartmentFilterToAll()
    {
        // The placeholder promises a search across every department, so leaving
        // a department filter applied underneath would quietly contradict it.
        IRenderedComponent<TeamPage> cut = RenderTeam();

        IElement sales = cut.FindAll(".app-deptfilter__row")
            .First(r => r.TextContent.Contains("Sales", StringComparison.Ordinal));
        await sales.ClickAsync(new());

        await Search(cut).InputAsync(new() { Value = "Maria" });

        cut.WaitForAssertion(() =>
        {
            cut.Find(".app-deptfilter__row--selected").TextContent.Should().Contain("All");
            cut.FindAll(".app-empcard").Should().ContainSingle();
        });
    }

    [Fact]
    public async Task TeamPage_ChoosingADepartmentClearsTheSearchTerm()
    {
        IRenderedComponent<TeamPage> cut = RenderTeam();
        await Search(cut).InputAsync(new() { Value = "Sarah" });
        cut.WaitForAssertion(() => cut.FindAll(".app-empcard").Should().ContainSingle());

        IElement enablement = cut.FindAll(".app-deptfilter__row")
            .First(r => r.TextContent.Contains("Business Enablement", StringComparison.Ordinal));
        await enablement.ClickAsync(new());

        cut.WaitForAssertion(() => cut.FindAll(".app-empcard").Should().HaveCount(8));
    }
}

public class EmployeeCardTests : ComponentTestBase
{
    private static EmployeeCardDto Card(
        string name = "David Chen",
        string? role = "Solutions Developer",
        string? photo = null,
        StaffType type = StaffType.Employee) =>
        new(1, name, role, "Business Enablement", "#44B4A6", photo, type);

    [Fact]
    public void EmployeeCard_WithNoPhoto_ShowsTheInitials()
    {
        IRenderedComponent<EmployeeCard> cut = Render<EmployeeCard>(p => p
            .Add(x => x.Employee, Card()));

        cut.Find(".app-empcard__initials").TextContent.Trim().Should().Be("DC");
    }

    [Fact]
    public void EmployeeCard_WithAPhoto_ShowsTheImageWithAltText()
    {
        IRenderedComponent<EmployeeCard> cut = Render<EmployeeCard>(p => p
            .Add(x => x.Employee, Card(photo: "/media/david.png")));

        cut.FindAll(".app-empcard__initials").Should().BeEmpty();
        cut.Find("img").GetAttribute("alt").Should().Be("Photo of David Chen");
    }

    [Fact]
    public void EmployeeCard_ForAnEmployee_ShowsTheEmployeeChip()
    {
        IRenderedComponent<EmployeeCard> cut = Render<EmployeeCard>(p => p
            .Add(x => x.Employee, Card()));

        cut.Find(".app-empcard__chip").TextContent.Should().Contain("Employee");
    }

    [Fact]
    public void EmployeeCard_ForManagement_ShowsTheManagementChip()
    {
        IRenderedComponent<EmployeeCard> cut = Render<EmployeeCard>(p => p
            .Add(x => x.Employee, Card(type: StaffType.Management)));

        cut.Find(".app-empcard__chip").TextContent.Should().Contain("Management");
    }

    [Fact]
    public void EmployeeCard_WithNoJobTitle_OmitsTheRoleLine()
    {
        IRenderedComponent<EmployeeCard> cut = Render<EmployeeCard>(p => p
            .Add(x => x.Employee, Card(role: null)));

        cut.FindAll(".app-empcard__role").Should().BeEmpty();
    }

    [Fact]
    public void EmployeeCard_WithASingleWordName_StillProducesInitials()
    {
        IRenderedComponent<EmployeeCard> cut = Render<EmployeeCard>(p => p
            .Add(x => x.Employee, Card(name: "Sarah")));

        cut.Find(".app-empcard__initials").TextContent.Trim().Should().Be("S");
    }
}

public class DepartmentFilterTests : ComponentTestBase
{
    private static readonly IReadOnlyList<DepartmentListItemDto> Sample =
    [
        new(1, "Sales", "#E55218", 3),
        new(2, "Accounts", "#9A8445", 0)
    ];

    [Fact]
    public void DepartmentFilter_SumsTheAllCountFromTheRows()
    {
        // Derived rather than fetched, so it can never disagree with the rows.
        IRenderedComponent<DepartmentFilter> cut = Render<DepartmentFilter>(p => p
            .Add(x => x.Departments, Sample));

        cut.Find(".app-deptfilter__row .app-deptfilter__count")
            .TextContent.Trim().Should().Be("(3)");
    }

    [Fact]
    public void DepartmentFilter_WithNoEditHandler_RendersNoPencils()
    {
        IRenderedComponent<DepartmentFilter> cut = Render<DepartmentFilter>(p => p
            .Add(x => x.Departments, Sample));

        cut.FindAll(".app-deptfilter__edit").Should().BeEmpty();
    }

    [Fact]
    public void DepartmentFilter_WithAnEditHandler_RendersALabelledPencilPerRow()
    {
        IRenderedComponent<DepartmentFilter> cut = Render<DepartmentFilter>(p => p
            .Add(x => x.Departments, Sample)
            .Add(x => x.OnEdit, EventCallback.Factory.Create<int>(this, _ => { })));

        cut.FindAll(".app-deptfilter__edit").Should().HaveCount(2);
        cut.FindAll(".app-deptfilter__edit").Should()
            .OnlyContain(b => b.GetAttribute("aria-label")!.StartsWith("Edit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DepartmentFilter_OnEdit_PassesTheDepartmentId()
    {
        int? edited = null;

        IRenderedComponent<DepartmentFilter> cut = Render<DepartmentFilter>(p => p
            .Add(x => x.Departments, Sample)
            .Add(x => x.OnEdit, EventCallback.Factory.Create<int>(this, id => edited = id)));

        await cut.FindAll(".app-deptfilter__edit")[1].ClickAsync(new());

        edited.Should().Be(2);
    }

    [Fact]
    public async Task DepartmentFilter_OnSelect_RaisesTheChangeWithTheId()
    {
        int? selected = -1;

        IRenderedComponent<DepartmentFilter> cut = Render<DepartmentFilter>(p => p
            .Add(x => x.Departments, Sample)
            .Add(x => x.SelectedDepartmentIdChanged,
                EventCallback.Factory.Create<int?>(this, id => selected = id)));

        await cut.FindAll(".app-deptfilter__row")[1].ClickAsync(new());

        selected.Should().Be(1);
    }

    [Fact]
    public async Task DepartmentFilter_OnSelectingAll_RaisesTheChangeWithNull()
    {
        int? selected = 99;

        IRenderedComponent<DepartmentFilter> cut = Render<DepartmentFilter>(p => p
            .Add(x => x.Departments, Sample)
            .Add(x => x.SelectedDepartmentId, 1)
            .Add(x => x.SelectedDepartmentIdChanged,
                EventCallback.Factory.Create<int?>(this, id => selected = id)));

        await cut.FindAll(".app-deptfilter__row")[0].ClickAsync(new());

        selected.Should().BeNull();
    }
}
