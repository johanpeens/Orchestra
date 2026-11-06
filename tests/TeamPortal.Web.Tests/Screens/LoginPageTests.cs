using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Web.Components.Pages;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public class LoginPageTests : ComponentTestBase
{
    private const string GoodEmail = "admin@company.com";
    private const string GoodPassword = "Portal!2026";

    private static IElement Email(IRenderedComponent<LoginPage> cut) => cut.Find("#email");

    private static IElement Password(IRenderedComponent<LoginPage> cut) => cut.Find("#password");

    private static IElement SubmitButton(IRenderedComponent<LoginPage> cut) =>
        cut.Find("button[type=submit]");

    // Immediate="true" binds MudTextField to oninput, not onchange.
    private static async Task FillAsync(IRenderedComponent<LoginPage> cut, string email, string password)
    {
        await Email(cut).InputAsync(new() { Value = email });
        await Password(cut).InputAsync(new() { Value = password });
    }

    // --- Layout ---------------------------------------------------------------

    [Fact]
    public void LoginPage_RendersTheBrandPanelAndTheForm()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        cut.FindAll(".app-login__panel").Should().ContainSingle();
        cut.FindAll(".app-login__form").Should().ContainSingle();
    }

    [Fact]
    public void LoginPage_ShowsTheWelcomeCopyFromTheDesign()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        cut.Find(".app-login__title").TextContent.Should().Contain("Welcome to the Orchestra Bot Training Portal");
        cut.Find(".app-login__subtitle").TextContent
            .Should().Contain("Welcome back! Please enter your details below.");
    }

    [Fact]
    public void LoginPage_ShowsTheBrandPitch()
    {
        // The old line - "orchestrate your enterprise operations in one unified
        // workspace" - described a category rather than this product. The pitch
        // now says what actually happens here.
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        cut.Find(".app-login__pitch-title").TextContent
            .Should().Contain("playing from the same sheet");
    }

    [Fact]
    public void LoginPage_HasRememberMeAndForgotPassword()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        cut.Markup.Should().Contain("Remember me");
        cut.Find(".app-login__forgot").TextContent.Should().Contain("Forgot password?");
    }

    [Fact]
    public void LoginPage_OffersAWayToRegister()
    {
        // Was "Contact Admin" and a mailto: link until registration existed.
        // Now it points at the form.
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        cut.Find(".app-login__footer").TextContent.Should().Contain("Create one");
        cut.Find(".app-login__footer a").GetAttribute("href").Should().Be("/register");
    }

    [Fact]
    public void LoginPage_LinksToForgotPassword()
    {
        // The link has been on the page since phase A4 and pointed at nothing
        // until the page existed. This is the test that would have caught that.
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        cut.Find(".app-login__forgot").GetAttribute("href").Should().Be("/forgot-password");
    }

    // --- Submit button state --------------------------------------------------

    [Fact]
    public void LoginPage_OnFirstRender_TheSubmitButtonIsDisabled()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        SubmitButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task LoginPage_WithOnlyAnEmail_TheSubmitButtonStaysDisabled()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await Email(cut).InputAsync(new() { Value = GoodEmail });

        SubmitButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task LoginPage_WithBothFieldsFilled_TheSubmitButtonIsEnabled()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await FillAsync(cut, GoodEmail, GoodPassword);

        SubmitButton(cut).HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task LoginPage_WithAWhitespaceOnlyPassword_TheSubmitButtonStaysDisabled()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await FillAsync(cut, GoodEmail, "   ");

        SubmitButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    // --- Password reveal ------------------------------------------------------

    [Fact]
    public void LoginPage_ByDefault_ThePasswordIsMasked()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        Password(cut).GetAttribute("type").Should().Be("password");
    }

    [Fact]
    public async Task LoginPage_OnReveal_ThePasswordBecomesVisible()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await cut.Find(".app-login__reveal button").ClickAsync(new());

        Password(cut).GetAttribute("type").Should().Be("text");
    }

    [Fact]
    public void LoginPage_TheRevealButtonHasAnAccessibleName()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        cut.Find(".app-login__reveal button")
            .GetAttribute("aria-label").Should().Be("Show password");
    }

    [Fact]
    public async Task LoginPage_TheRevealButtonLabelFlipsWhenToggled()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await cut.Find(".app-login__reveal button").ClickAsync(new());

        cut.Find(".app-login__reveal button")
            .GetAttribute("aria-label").Should().Be("Hide password");
    }

    // --- Sign in --------------------------------------------------------------

    [Fact]
    public async Task LoginPage_WithCorrectCredentials_SignsTheUserIn()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await FillAsync(cut, GoodEmail, GoodPassword);
        await SubmitButton(cut).ClickAsync(new());

        Services.GetRequiredService<ICurrentUserService>().IsSignedIn.Should().BeTrue();
    }

    [Fact]
    public async Task LoginPage_WithCorrectCredentials_NavigatesToTheDashboard()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        await FillAsync(cut, GoodEmail, GoodPassword);
        await SubmitButton(cut).ClickAsync(new());

        navigation.Uri.Should().EndWith("/");
    }

    [Fact]
    public async Task LoginPage_WithTheWrongPassword_ShowsAnError()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await FillAsync(cut, GoodEmail, "wrong-password");
        await SubmitButton(cut).ClickAsync(new());

        cut.Find("[role=alert]").TextContent
            .Should().Contain("do not match");
    }

    [Fact]
    public async Task LoginPage_WhenSignInFails_KeepsTheEmailButClearsThePassword()
    {
        // Retyping the address after a typo in the password is pure friction.
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await FillAsync(cut, GoodEmail, "wrong-password");
        await SubmitButton(cut).ClickAsync(new());

        Email(cut).GetAttribute("value").Should().Be(GoodEmail);
        Password(cut).GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task LoginPage_WithAnUnknownEmail_ShowsTheSameErrorAsAWrongPassword()
    {
        // Two different messages would let an attacker enumerate accounts.
        IRenderedComponent<LoginPage> unknown = Render<LoginPage>();
        await FillAsync(unknown, "nobody@company.com", "whatever");
        await SubmitButton(unknown).ClickAsync(new());
        string unknownMessage = unknown.Find("[role=alert]").TextContent.Trim();

        IRenderedComponent<LoginPage> wrong = Render<LoginPage>();
        await FillAsync(wrong, GoodEmail, "wrong-password");
        await SubmitButton(wrong).ClickAsync(new());
        string wrongMessage = wrong.Find("[role=alert]").TextContent.Trim();

        unknownMessage.Should().Be(wrongMessage);
    }

    [Fact]
    public async Task LoginPage_WithAMalformedEmail_ShowsAFieldError()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await FillAsync(cut, "not-an-address", GoodPassword);
        await SubmitButton(cut).ClickAsync(new());

        cut.Markup.Should().Contain("does not look like an email address");
        Services.GetRequiredService<ICurrentUserService>().IsSignedIn.Should().BeFalse();
    }

    [Fact]
    public async Task LoginPage_AfterAnError_EditingAFieldClearsIt()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();
        await FillAsync(cut, GoodEmail, "wrong-password");
        await SubmitButton(cut).ClickAsync(new());
        cut.FindAll("[role=alert]").Should().NotBeEmpty();

        await Password(cut).InputAsync(new() { Value = "another-try" });

        cut.FindAll("[role=alert]").Should().BeEmpty();
    }

    [Fact]
    public async Task LoginPage_AfterFiveFailures_ShowsTheLockoutMessage()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await FillAsync(cut, GoodEmail, "wrong-password");
            await SubmitButton(cut).ClickAsync(new());
        }

        cut.Find("[role=alert]").TextContent.Should().Contain("Too many failed attempts");
    }

    [Fact]
    public async Task LoginPage_DoesNotSignInBeforeTheButtonIsPressed()
    {
        IRenderedComponent<LoginPage> cut = Render<LoginPage>();

        await FillAsync(cut, GoodEmail, GoodPassword);

        Services.GetRequiredService<ICurrentUserService>().IsSignedIn.Should().BeFalse();
    }

    // --- Return URL -----------------------------------------------------------

    [Fact]
    public async Task LoginPage_WithASafeReturnUrl_NavigatesThere()
    {
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo("/login?returnUrl=%2Fteam");

        IRenderedComponent<LoginPage> cut = Render<LoginPage>();
        await FillAsync(cut, GoodEmail, GoodPassword);
        await SubmitButton(cut).ClickAsync(new());

        navigation.Uri.Should().EndWith("/team");
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    public async Task LoginPage_WithAnOffSiteReturnUrl_GoesToTheDashboardInstead(string hostile)
    {
        // An open redirect on a login page hands an attacker a credible phishing
        // hop: the link really is our domain, right up until it is not.
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo($"/login?returnUrl={Uri.EscapeDataString(hostile)}");

        IRenderedComponent<LoginPage> cut = Render<LoginPage>();
        await FillAsync(cut, GoodEmail, GoodPassword);
        await SubmitButton(cut).ClickAsync(new());

        navigation.Uri.Should().NotContain("evil.example");
    }
}
