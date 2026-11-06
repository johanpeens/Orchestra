using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Web.Components.Layout;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Shell;

public class SectionSwitcherTests : ComponentTestBase
{
    [Fact]
    public void SectionSwitcher_LinksToEverySection()
    {
        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.FindAll("a").Select(a => a.GetAttribute("href"))
            .Should().BeEquivalentTo(["/processes", "/team", "/chat"]);
    }

    [Fact]
    public void SectionSwitcher_TheTeamLinkIsVisibleWithoutOpeningAnything()
    {
        // The whole point of the fix: no menu to discover, no click to reveal it.
        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.Markup.Should().Contain("Team");
        cut.Find("a[href='/team']").TextContent.Should().Contain("Team");
    }

    [Fact]
    public void SectionSwitcher_OnTheProcessSide_MarksBusinessProcessAsCurrent()
    {
        // Has to navigate first. The root is the landing page now, so rendering
        // at "/" no longer puts you on the process side.
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo("/processes");

        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.Find("a[href='/processes']").GetAttribute("aria-current").Should().Be("page");
        cut.Find("a[href='/team']").HasAttribute("aria-current").Should().BeFalse();
    }

    [Fact]
    public void SectionSwitcher_OnTheTeamSide_MarksTeamAsCurrent()
    {
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo("/team");

        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.Find("a[href='/team']").GetAttribute("aria-current").Should().Be("page");
        cut.Find("a[href='/processes']").HasAttribute("aria-current").Should().BeFalse();
    }

    [Fact]
    public void SectionSwitcher_HighlightsTheActiveSection()
    {
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo("/processes");

        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.Find("a[href='/processes']").ClassList.Should().Contain("app-pill--active");
        cut.Find("a[href='/team']").ClassList.Should().NotContain("app-pill--active");
    }

    [Fact]
    public void SectionSwitcher_OffersEverySection()
    {
        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.FindAll(".app-pill").Select(e => e.TextContent.Trim())
            .Should().Equal("Team", "Business Process", "Chat");
    }

    [Fact]
    public void SectionSwitcher_TheChatPill_PointsAtTheChatRoute()
    {
        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.FindAll(".app-pill")[2].GetAttribute("href").Should().Be("/chat");
    }

    [Fact]
    public void SectionSwitcher_AtTheRoot_MarksNothingAsCurrent()
    {
        // The root is the landing page now, and it has no pill - so no pill is
        // the current one. Highlighting Business Process there would say you
        // were somewhere you are not.
        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.FindAll(".app-pill")
            .Should().AllSatisfy(p => p.HasAttribute("aria-current").Should().BeFalse());
    }

    [Fact]
    public void SectionSwitcher_TheBarReadsTeamProcessChatPackage()
    {
        // The order is a decision, not an accident of the order they were added.
        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.FindAll(".app-pill").Select(e => e.GetAttribute("href"))
            .Should().Equal("/team", "/processes", "/chat");
    }

    [Fact]
    public void SectionSwitcher_DoesNotCarryTheTrainingData()
    {
        // It is not a section of the app - it is the material behind the chat,
        // reached by a button on the chat itself.
        IRenderedComponent<SectionSwitcher> cut = Render<SectionSwitcher>();

        cut.Markup.Should().NotContain("/package");
    }

    [Theory]
    [InlineData("", AppSection.Home)]
    [InlineData("processes", AppSection.Process)]
    [InlineData("processes/1", AppSection.Process)]
    [InlineData("team", AppSection.Team)]
    [InlineData("chat", AppSection.Chat)]
    [InlineData("/chat", AppSection.Chat)]
    [InlineData("CHAT", AppSection.Chat)]
    [InlineData("package", AppSection.Package)]
    // The page kept its original route, and the {} button on the process screen
    // still uses it, so both spellings have to land on the same section.
    [InlineData("context", AppSection.Package)]
    // An unknown route falls back to Home, and so does the chrome.
    [InlineData("register", AppSection.Home)]
    public void AppSections_ReadsTheSectionOffThePath(string path, AppSection expected) =>
        AppSections.FromPath(path).Should().Be(expected);

    [Fact]
    public void AppSections_OnlyTheProcessSideGetsTheFolderRail()
    {
        // Team and Chat have nothing to list. An empty 264px rail is worse than
        // no rail.
        AppSection.Process.HasFolderRail().Should().BeTrue();
        AppSection.Team.HasFolderRail().Should().BeFalse();
        AppSection.Chat.HasFolderRail().Should().BeFalse();
        AppSection.Package.HasFolderRail().Should().BeFalse();
    }
}
