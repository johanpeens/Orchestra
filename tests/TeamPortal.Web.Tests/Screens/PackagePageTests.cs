using AngleSharp.Dom;
using Bunit;
using TeamPortal.Web.Components.Pages;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public class PackagePageTests : ComponentTestBase
{
    private IRenderedComponent<ContextPage> RenderPackage()
    {
        IRenderedComponent<ContextPage> cut = Render<ContextPage>();
        cut.WaitForAssertion(() => cut.FindAll(".app-context__tree").Should().NotBeEmpty());
        return cut;
    }

    [Fact]
    public void PackagePage_ArrivesCompletelyCollapsed()
    {
        // The whole point. Not one row open, including the everything-blob.
        IRenderedComponent<ContextPage> cut = RenderPackage();

        cut.FindAll(".app-context__node").Should().NotBeEmpty();
        cut.FindAll(".app-context__node")
            .Should().AllSatisfy(n => n.HasAttribute("open").Should().BeFalse());
    }

    [Fact]
    public void PackagePage_IsHeadedMyTrainingData()
    {
        // Named for what it is TO THE READER. "System context" was the name
        // while this was a drill-down from the process screen, "Grounding" is
        // what the technique is called, and neither tells somebody arriving
        // from the chat that this is the stuff their bot answers from.
        IRenderedComponent<ContextPage> cut = RenderPackage();

        cut.Find(".app-context__title").TextContent.Trim().Should().Be("My Training Data");
    }

    [Fact]
    public void PackagePage_ListsEveryFolderAsARow()
    {
        IRenderedComponent<ContextPage> cut = RenderPackage();

        cut.Markup.Should().Contain("Client Onboarding");
    }

    [Fact]
    public void PackagePage_KeepsTheWholeObjectAvailable()
    {
        // Copy JSON is the job this page was built for; breaking the blob into a
        // tree must not take the blob away.
        IRenderedComponent<ContextPage> cut = RenderPackage();

        cut.FindAll(".app-context__node--all").Should().ContainSingle();
        cut.Find(".app-context__node--all .app-context__json").TextContent
            .Should().Contain("\"folders\"");
    }

    [Fact]
    public void PackagePage_EveryRowSaysWhatIsInsideItBeforeYouOpenIt()
    {
        // A row you have to open to find out whether it is worth opening is not
        // saving anybody anything.
        IRenderedComponent<ContextPage> cut = RenderPackage();

        cut.FindAll(".app-context__summary")
            .Should().AllSatisfy(su => su.TextContent.Trim().Should().NotBeEmpty());
    }

    [Fact]
    public void PackagePage_TheFolderRow_CountsItsProcesses()
    {
        IRenderedComponent<ContextPage> cut = RenderPackage();

        // Not the everything-blob - its JSON contains every folder name too.
        IElement folder = cut.FindAll(".app-context__node:not(.app-context__node--all)")
            .First(n => n.TextContent.Contains("Client Onboarding", StringComparison.Ordinal));

        folder.QuerySelector(".app-context__nodemeta")!.TextContent
            .Should().Contain("processes");
    }

    [Fact]
    public void PackagePage_NestsStepsInsideProcessesInsideFolders()
    {
        // Three deep. Flat would be the wall again with extra clicks.
        IRenderedComponent<ContextPage> cut = RenderPackage();

        cut.FindAll(".app-context__node .app-context__node .app-context__node")
            .Should().NotBeEmpty();
    }

    [Fact]
    public void PackagePage_StillOffersToCopyTheJson()
    {
        IRenderedComponent<ContextPage> cut = RenderPackage();

        cut.Markup.Should().Contain("Copy JSON");
    }

    [Fact]
    public void PackagePage_TheRowsAreRealDisclosures()
    {
        // <details>, so they work with the keyboard, with find-in-page, and
        // without a circuit round trip per click.
        IRenderedComponent<ContextPage> cut = RenderPackage();

        cut.FindAll(".app-context__node")
            .Should().AllSatisfy(n => n.NodeName.Should().Be("DETAILS"));

        cut.FindAll(".app-context__summary")
            .Should().AllSatisfy(su => su.NodeName.Should().Be("SUMMARY"));
    }
}
