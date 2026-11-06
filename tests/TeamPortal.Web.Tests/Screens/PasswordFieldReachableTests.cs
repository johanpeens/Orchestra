using System.Text.RegularExpressions;
using TeamPortal.Web.Components.Pages;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public partial class PasswordFieldReachableTests : ComponentTestBase
{
    /// <summary>
    /// The stylesheet as shipped. Read from disk rather than from a constant,
    /// because the bug was in the CSS and a copy of the CSS would not have it.
    /// </summary>
    private static string Stylesheet => File.ReadAllText(FindStylesheet());

    [Fact]
    public void TheRevealStrip_DoesNotSwallowClicks()
    {
        Match block = RevealRule().Match(Stylesheet);

        block.Success.Should().BeTrue(".app-login__reveal should exist in app.css");

        block.Value.Should().Contain(
            "pointer-events: none",
            "the strip lies on top of the password input, so it has to let clicks through");
    }

    [Fact]
    public void TheRevealButton_TakesClicksBack()
    {
        // pointer-events: none on the strip alone would make the eye icon dead
        // too. The child rule is what keeps it working.
        Stylesheet.Should().Contain(".app-login__reveal > *");

        Match child = RevealChildRule().Match(Stylesheet);

        child.Success.Should().BeTrue();
        child.Value.Should().Contain("pointer-events: auto");
    }

    [Fact]
    public void TheRevealStrip_IsStillPulledOverTheField()
    {
        // If somebody "fixes" this by removing the negative margin instead, the
        // button drops below the box and the layout no longer matches the design.
        // Both halves have to stay.
        Match block = RevealRule().Match(Stylesheet);

        block.Value.Should().Contain("margin-top: -");
    }

    [Fact]
    public void EveryScreenWithAPasswordBox_UsesTheSameStrip()
    {
        // Three screens have a password field. All three use this strip, so the
        // rules above either fix all three or break all three - which is the
        // reason to keep them on one class rather than styling each page.
        Render<LoginPage>().Markup.Should().Contain("app-login__reveal");
        Render<RegisterPage>().Markup.Should().Contain("app-login__reveal");
        Render<ResetPasswordPage>().Markup.Should().Contain("app-login__reveal");
    }

    private static string FindStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the test has to be able to find the repository root");

        return Path.Combine(
            directory!.FullName, "src", "TeamPortal.Web", "wwwroot", "app.css");
    }

    [GeneratedRegex(@"\.app-login__reveal\s*\{[^}]*\}", RegexOptions.None, 1000)]
    private static partial Regex RevealRule();

    [GeneratedRegex(@"\.app-login__reveal\s*>\s*\*\s*\{[^}]*\}", RegexOptions.None, 1000)]
    private static partial Regex RevealChildRule();
}
