using Bunit;
using Microsoft.AspNetCore.Components;
using TeamPortal.Web.Components.Shared;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Shell;

public class AppPageHeaderTests : ComponentTestBase
{
    [Fact]
    public void AppPageHeader_WithOverlineAndTitle_RendersBoth()
    {
        IRenderedComponent<AppPageHeader> cut = Render<AppPageHeader>(p => p
            .Add(x => x.Overline, "Welcome To Your")
            .Add(x => x.Title, "Dashboard"));

        cut.Find(".app-page-header__overline").TextContent.Should().Contain("Welcome To Your");
        cut.Find(".app-page-header__title").TextContent.Should().Contain("Dashboard");
    }

    [Fact]
    public void AppPageHeader_WithoutATitle_Throws()
    {
        // A blank banner shipping to production is worse than a loud failure here.
        Action act = () => Render<AppPageHeader>(p => p
            .Add(x => x.Overline, "You are viewing")
            .Add(x => x.Title, "   "));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AppPageHeader_WithoutAnOverline_RendersJustTheTitle()
    {
        // Overline is optional: docs/design/02 and docs/design/05 use a description line
        // instead. Only the title is mandatory.
        IRenderedComponent<AppPageHeader> cut = Render<AppPageHeader>(p => p
            .Add(x => x.Title, "Team"));

        cut.FindAll(".app-page-header__overline").Should().BeEmpty();
        cut.Find(".app-page-header__title").TextContent.Should().Contain("Team");
    }

    [Fact]
    public void AppPageHeader_WithADescription_RendersIt()
    {
        IRenderedComponent<AppPageHeader> cut = Render<AppPageHeader>(p => p
            .Add(x => x.Title, "Team")
            .Add(x => x.Description, "Manage and view members."));

        cut.Find(".app-page-header__description").TextContent
            .Should().Contain("Manage and view members.");
    }

    [Fact]
    public void AppPageHeader_WithNoChildContent_RendersNoActionsSlot()
    {
        IRenderedComponent<AppPageHeader> cut = Render<AppPageHeader>(p => p
            .Add(x => x.Overline, "You are viewing")
            .Add(x => x.Title, "Clients"));

        cut.FindAll(".app-page-header__actions").Should().BeEmpty();
    }

    [Fact]
    public void AppPageHeader_WithChildContent_RendersTheActionsSlot()
    {
        IRenderedComponent<AppPageHeader> cut = Render<AppPageHeader>(p => p
            .Add(x => x.Overline, "You are viewing")
            .Add(x => x.Title, "Clients")
            .AddChildContent("<button id=\"add-client\">Add Client</button>"));

        cut.Find(".app-page-header__actions").Should().NotBeNull();
        cut.Find("#add-client").TextContent.Should().Be("Add Client");
    }

    [Fact]
    public void AppPageHeader_UsesTheSharedCssClassesNotInlineStyles()
    {
        // Styling lives in app.css. An inline style here means the next theme
        // change silently skips this page.
        IRenderedComponent<AppPageHeader> cut = Render<AppPageHeader>(p => p
            .Add(x => x.Overline, "Welcome To Your")
            .Add(x => x.Title, "Dashboard"));

        cut.Find(".app-page-header").HasAttribute("style").Should().BeFalse();
    }
}

public class LoadingPanelTests : ComponentTestBase
{
    [Fact]
    public void LoadingPanel_WhenVisible_RendersTheSpinner()
    {
        IRenderedComponent<LoadingPanel> cut = Render<LoadingPanel>(p => p
            .Add(x => x.Visible, true));

        cut.FindAll(".app-loading").Should().ContainSingle();
    }

    [Fact]
    public void LoadingPanel_WhenNotVisible_RendersNothing()
    {
        IRenderedComponent<LoadingPanel> cut = Render<LoadingPanel>(p => p
            .Add(x => x.Visible, false));

        cut.FindAll(".app-loading").Should().BeEmpty();
    }

    [Fact]
    public void LoadingPanel_AnnouncesItselfToScreenReaders()
    {
        // A silent wait is a broken page for anyone who cannot see the spinner.
        IRenderedComponent<LoadingPanel> cut = Render<LoadingPanel>();

        var panel = cut.Find(".app-loading");
        panel.GetAttribute("role").Should().Be("status");
        panel.GetAttribute("aria-live").Should().Be("polite");
    }

    [Fact]
    public void LoadingPanel_ShowsTheSuppliedMessage()
    {
        IRenderedComponent<LoadingPanel> cut = Render<LoadingPanel>(p => p
            .Add(x => x.Message, "Loading folders..."));

        cut.Find(".app-loading__text").TextContent.Should().Contain("Loading folders...");
    }
}

public class EmptyStateTests : ComponentTestBase
{
    [Fact]
    public void EmptyState_RendersTheTitle()
    {
        IRenderedComponent<EmptyState> cut = Render<EmptyState>(p => p
            .Add(x => x.Title, "No employees yet"));

        cut.Find(".app-empty-state__title").TextContent.Should().Contain("No employees yet");
    }

    [Fact]
    public void EmptyState_WithNoDescription_RendersNoDescription()
    {
        IRenderedComponent<EmptyState> cut = Render<EmptyState>(p => p
            .Add(x => x.Title, "No employees yet"));

        cut.FindAll(".app-empty-state__description").Should().BeEmpty();
    }

    [Fact]
    public void EmptyState_WithADescription_RendersIt()
    {
        IRenderedComponent<EmptyState> cut = Render<EmptyState>(p => p
            .Add(x => x.Title, "No employees yet")
            .Add(x => x.Description, "Add the first person to this department."));

        cut.Find(".app-empty-state__description").TextContent
            .Should().Contain("Add the first person");
    }

    [Fact]
    public void EmptyState_WithChildContent_RendersTheCallToAction()
    {
        IRenderedComponent<EmptyState> cut = Render<EmptyState>(p => p
            .Add(x => x.Title, "No employees yet")
            .AddChildContent("<button id=\"add\">Add Employee</button>"));

        cut.Find("#add").Should().NotBeNull();
    }
}

public class NavMenuTests : ComponentTestBase
{
    private IRenderedComponent<Web.Components.Layout.NavMenu> RenderNav(
        Action<Bunit.ComponentParameterCollectionBuilder<Web.Components.Layout.NavMenu>>? p = null)
    {
        IRenderedComponent<Web.Components.Layout.NavMenu> cut =
            p is null ? Render<Web.Components.Layout.NavMenu>() : Render(p);

        cut.WaitForAssertion(() => cut.FindAll(".app-sidebar__row").Should().NotBeEmpty());
        return cut;
    }

    [Fact]
    public void NavMenu_RendersARowPerSeededFolder()
    {
        var cut = RenderNav();

        cut.FindAll(".app-sidebar__row").Should().HaveCount(2);
        cut.Markup.Should().Contain("The Business").And.Contain("Client Processes");
    }

    [Fact]
    public void NavMenu_ShowsTheProcessCountPerFolder()
    {
        // The seed puts one process in "Client Processes" and none in "The Business".
        var cut = RenderNav();

        cut.FindAll(".app-sidebar__count").Select(e => e.TextContent.Trim())
            .Should().BeEquivalentTo(["0", "1"]);
    }

    [Fact]
    public void NavMenu_UsesTheProcessesHeading()
    {
        // docs/design/02 - the sidebar heading is a small uppercase label.
        var cut = RenderNav();

        cut.Find(".app-sidebar__heading").TextContent.Trim().Should().Be("Processes");
    }

    [Fact]
    public void NavMenu_PutsTheNewProcessButtonBesideTheHeading()
    {
        // docs/design/02 has the heading and the action on ONE row, not stacked.
        var cut = RenderNav();

        var head = cut.Find(".app-sidebar__head");
        head.QuerySelector(".app-sidebar__heading").Should().NotBeNull();
        head.QuerySelector("button").Should().NotBeNull();
        head.TextContent.Should().Contain("New Process");
    }

    [Fact]
    public void NavMenu_PinsViewProcessHistoryToTheBottom()
    {
        var cut = RenderNav();

        cut.Find(".app-sidebar__foot").TextContent.Should().Contain("View Process History");
    }

    [Fact]
    public void NavMenu_HasNoTeamOrActivityLinks()
    {
        // The designs keep the sidebar to folders only. Moving between the
        // process side and the team side happens via the app bar pill.
        var cut = RenderNav();

        cut.Markup.Should().NotContain("href=\"/team\"");
        cut.Markup.Should().NotContain("href=\"/activity\"");
    }

    [Fact]
    public void NavMenu_WithNoSelection_HighlightsNothing()
    {
        var cut = RenderNav();

        cut.FindAll(".app-sidebar__row--selected").Should().BeEmpty();
    }

    [Fact]
    public void NavMenu_WithASelectedFolder_HighlightsThatRow()
    {
        var cut = RenderNav(p => p.Add(x => x.SelectedFolderId, 2));

        cut.FindAll(".app-sidebar__row--selected").Should().ContainSingle()
            .Which.TextContent.Should().Contain("Client Processes");
    }

    [Fact]
    public void NavMenu_MarksTheSelectedRowForScreenReaders()
    {
        var cut = RenderNav(p => p.Add(x => x.SelectedFolderId, 2));

        cut.Find(".app-sidebar__row--selected").GetAttribute("aria-current").Should().Be("page");
    }

    [Fact]
    public async Task NavMenu_OnNewProcess_RaisesTheCallback()
    {
        bool raised = false;

        var cut = RenderNav(p => p
            .Add(x => x.OnNewProcess, EventCallback.Factory.Create(this, () => raised = true)));

        await cut.Find(".app-sidebar__head button").ClickAsync(new());

        raised.Should().BeTrue();
    }

    [Fact]
    public async Task NavMenu_OnViewHistory_RaisesTheCallback()
    {
        bool raised = false;

        var cut = RenderNav(p => p
            .Add(x => x.OnViewHistory, EventCallback.Factory.Create(this, () => raised = true)));

        await cut.Find(".app-sidebar__foot button").ClickAsync(new());

        raised.Should().BeTrue();
    }
}

public class AccessibilityTests : ComponentTestBase
{
    [Fact]
    public void NotificationBell_HasAnAccessibleName()
    {
        // Every icon button needs one - the icon alone announces nothing.
        IRenderedComponent<Web.Components.Layout.NotificationBell> cut =
            Render<Web.Components.Layout.NotificationBell>();

        cut.WaitForAssertion(() =>
            cut.Find("button").GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void NotificationBell_WithNoUnread_SaysSoInItsLabel()
    {
        IRenderedComponent<Web.Components.Layout.NotificationBell> cut =
            Render<Web.Components.Layout.NotificationBell>();

        cut.WaitForAssertion(() =>
            cut.Find("button").GetAttribute("aria-label").Should().Contain("none unread"));
    }

    [Fact]
    public void EveryIconButtonInTheShell_HasAnAriaLabel()
    {
        IRenderedComponent<Web.Components.Layout.NotificationBell> bell =
            Render<Web.Components.Layout.NotificationBell>();

        bell.WaitForAssertion(() =>
        {
            foreach (var button in bell.FindAll("button.mud-icon-button"))
            {
                button.GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace(
                    "an icon button with no label is invisible to a screen reader");
            }
        });
    }
}
