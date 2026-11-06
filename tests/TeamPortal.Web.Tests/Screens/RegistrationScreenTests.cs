using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Mock.Services;
using TeamPortal.Web.Components.Pages;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public class RegisterPageTests : ComponentTestBase
{
    private const string GoodPassword = "Portal!2026x";

    private static IElement SubmitButton(IRenderedComponent<RegisterPage> cut) =>
        cut.Find("button[type=submit]");

    [Fact]
    public void RegisterPage_OnFirstRender_TheSubmitButtonIsDisabled()
    {
        IRenderedComponent<RegisterPage> cut = Render<RegisterPage>();

        SubmitButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void RegisterPage_ShowsThePasswordRulesBeforeAnythingIsTyped()
    {
        // Telling somebody the rule after they break it is the worst time to.
        IRenderedComponent<RegisterPage> cut = Render<RegisterPage>();

        cut.Markup.Should().Contain("At least 10 characters");
    }

    [Fact]
    public void RegisterPage_LinksBackToSignIn()
    {
        IRenderedComponent<RegisterPage> cut = Render<RegisterPage>();

        cut.Find(".app-login__footer a").GetAttribute("href").Should().Be("/login");
    }

    [Fact]
    public async Task RegisterPage_WithAValidForm_ShowsTheCheckYourInboxScreen()
    {
        IRenderedComponent<RegisterPage> cut = Render<RegisterPage>();

        await FillAsync(cut, "Nomsa Dlamini", "nomsa@company.com", GoodPassword, GoodPassword);
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Check your inbox");
        cut.Markup.Should().Contain("nomsa@company.com");
    }

    [Fact]
    public async Task RegisterPage_WithAValidForm_SendsAConfirmationEmail()
    {
        IRenderedComponent<RegisterPage> cut = Render<RegisterPage>();
        MockEmailSender email = Services.GetRequiredService<MockEmailSender>();

        await FillAsync(cut, "Nomsa Dlamini", "nomsa@company.com", GoodPassword, GoodPassword);
        await cut.Find("form").SubmitAsync();

        email.Sent.Should().ContainSingle().Which.To.Should().Be("nomsa@company.com");
    }

    [Fact]
    public async Task RegisterPage_WhenThePasswordsDoNotMatch_StaysOnTheForm()
    {
        IRenderedComponent<RegisterPage> cut = Render<RegisterPage>();

        await FillAsync(cut, "Nomsa", "nomsa@company.com", GoodPassword, "Different!2026x");
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().NotContain("Check your inbox");
        cut.Markup.Should().Contain("do not match");
    }

    [Fact]
    public async Task RegisterPage_WhenThePasswordIsWeak_ShowsTheRuleRatherThanJustFailing()
    {
        IRenderedComponent<RegisterPage> cut = Render<RegisterPage>();

        await FillAsync(cut, "Nomsa", "nomsa@company.com", "short", "short");
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("At least 10 characters");
    }

    [Fact]
    public async Task RegisterPage_ForAnAddressThatAlreadyHasAnAccount_LooksExactlyTheSame()
    {
        // The screen must not become the user-enumeration tool the service
        // deliberately is not.
        IRenderedComponent<RegisterPage> cut = Render<RegisterPage>();

        await FillAsync(cut, "Impostor", "admin@company.com", GoodPassword, GoodPassword);
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Check your inbox");
        cut.Markup.Should().NotContain("already");
    }

    private static async Task FillAsync(
        IRenderedComponent<RegisterPage> cut,
        string name, string email, string password, string confirm)
    {
        // Immediate="true" binds these to oninput, so InputAsync is what the
        // component is actually listening for. ChangeAsync silently does nothing.
        await cut.Find("#fullName").InputAsync(new() { Value = name });
        await cut.Find("#email").InputAsync(new() { Value = email });
        await cut.Find("#password").InputAsync(new() { Value = password });
        await cut.Find("#confirmPassword").InputAsync(new() { Value = confirm });
    }
}

public class ForgotPasswordPageTests : ComponentTestBase
{
    [Fact]
    public async Task ForgotPasswordPage_ForAKnownAddress_ShowsTheCheckYourInboxScreen()
    {
        IRenderedComponent<ForgotPasswordPage> cut = Render<ForgotPasswordPage>();

        await cut.Find("#email").InputAsync(new() { Value = "admin@company.com" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Check your inbox");
    }

    [Fact]
    public async Task ForgotPasswordPage_ForAnUnknownAddress_LooksExactlyTheSame()
    {
        IRenderedComponent<ForgotPasswordPage> cut = Render<ForgotPasswordPage>();

        await cut.Find("#email").InputAsync(new() { Value = "nobody@company.com" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Check your inbox");
    }

    [Fact]
    public async Task ForgotPasswordPage_ForAKnownAddress_SendsAResetEmail()
    {
        IRenderedComponent<ForgotPasswordPage> cut = Render<ForgotPasswordPage>();
        MockEmailSender email = Services.GetRequiredService<MockEmailSender>();

        await cut.Find("#email").InputAsync(new() { Value = "admin@company.com" });
        await cut.Find("form").SubmitAsync();

        email.Sent.Should().ContainSingle()
            .Which.TextBody.Should().Contain("reset-password");
    }

    [Fact]
    public async Task ForgotPasswordPage_ForAnUnknownAddress_SendsNothing()
    {
        // The screen says the same thing either way; the mailbox is where the
        // difference lives, and there is no mailbox to send to here.
        IRenderedComponent<ForgotPasswordPage> cut = Render<ForgotPasswordPage>();
        MockEmailSender email = Services.GetRequiredService<MockEmailSender>();

        await cut.Find("#email").InputAsync(new() { Value = "nobody@company.com" });
        await cut.Find("form").SubmitAsync();

        email.Sent.Should().BeEmpty();
    }

    [Fact]
    public void ForgotPasswordPage_LinksBackToSignIn()
    {
        IRenderedComponent<ForgotPasswordPage> cut = Render<ForgotPasswordPage>();

        cut.Find(".app-login__footer a").GetAttribute("href").Should().Be("/login");
    }
}

public class ConfirmEmailPageTests : ComponentTestBase
{
    /// <summary>
    /// A SupplyParameterFromQuery parameter cannot be set the way an ordinary
    /// one can - bUnit refuses, and says to navigate instead. This is what
    /// clicking the link in the email actually does.
    /// </summary>
    private void NavigateWithLink(string email, string token)
    {
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo(
            $"/confirm-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}");
    }

    [Fact]
    public void ConfirmEmailPage_WithNoQueryString_SaysTheLinkIsIncomplete()
    {
        // Some mail clients wrap long URLs and lose the tail.
        IRenderedComponent<ConfirmEmailPage> cut = Render<ConfirmEmailPage>();

        cut.Markup.Should().Contain("incomplete");
    }

    [Fact]
    public async Task ConfirmEmailPage_WithAGoodToken_SaysConfirmed()
    {
        // Register through the service first, so the token is a real one.
        IAuthService auth = Services.GetRequiredService<IAuthService>();
        MockEmailSender email = Services.GetRequiredService<MockEmailSender>();

        await auth.RegisterAsync(new RegisterRequest(
            "Nomsa", "nomsa@company.com", "Portal!2026x", "Portal!2026x"));

        string token = MockEmailSender.TokenFrom(email.Sent[0])!;

        NavigateWithLink("nomsa@company.com", token);

        IRenderedComponent<ConfirmEmailPage> cut = Render<ConfirmEmailPage>();

        cut.Markup.Should().Contain("Email confirmed");
    }

    [Fact]
    public void ConfirmEmailPage_WithARubbishToken_SaysTheLinkIsNoLongerGood()
    {
        NavigateWithLink("nomsa@company.com", "not-a-token");

        IRenderedComponent<ConfirmEmailPage> cut = Render<ConfirmEmailPage>();

        cut.Markup.Should().Contain("no longer good");
    }

    [Fact]
    public async Task ConfirmEmailPage_OnSuccess_ExplainsThatApprovalIsStillToCome()
    {
        // Otherwise somebody confirms, tries to sign in, and is told to wait with
        // no idea that was always the plan.
        IAuthService auth = Services.GetRequiredService<IAuthService>();
        MockEmailSender email = Services.GetRequiredService<MockEmailSender>();

        await auth.RegisterAsync(new RegisterRequest(
            "Nomsa", "nomsa@company.com", "Portal!2026x", "Portal!2026x"));

        string token = MockEmailSender.TokenFrom(email.Sent[0])!;

        NavigateWithLink("nomsa@company.com", token);

        IRenderedComponent<ConfirmEmailPage> cut = Render<ConfirmEmailPage>();

        cut.Markup.Should().Contain("approve");
    }
}

public class RegistrationsPageTests : ComponentTestBase
{
    [Fact]
    public async Task RegistrationsPage_ForAnAdmin_ListsWhoIsWaiting()
    {
        await RegisterAndConfirmAsync();
        SignInAsAdmin();

        IRenderedComponent<RegistrationsPage> cut = Render<RegistrationsPage>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading accounts"));

        cut.Markup.Should().Contain("nomsa@company.com");
        cut.Markup.Should().Contain("Approve");
    }

    [Fact]
    public async Task RegistrationsPage_ForANonAdmin_ShowsNothing()
    {
        // The page has [Authorize(Roles = Admin)] as well, but bUnit renders the
        // component directly - so this is checking the service's own refusal,
        // which is the half that actually protects anything.
        await RegisterAndConfirmAsync();
        SignInAs(AppRoles.Manager);

        IRenderedComponent<RegistrationsPage> cut = Render<RegistrationsPage>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading accounts"));

        cut.Markup.Should().NotContain("nomsa@company.com");
    }

    [Fact]
    public async Task RegistrationsPage_WhenNobodyIsWaiting_ShowsAnEmptyState()
    {
        SignInAsAdmin();

        IRenderedComponent<RegistrationsPage> cut = Render<RegistrationsPage>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading accounts"));

        cut.Markup.Should().Contain("Nobody is waiting");
        await Task.CompletedTask;
    }

    private void SignInAsAdmin() => SignInAs(AppRoles.Admin);

    private void SignInAs(string role) =>
        Services.GetRequiredService<MockCurrentUserService>()
            .SetUser(new CurrentUserDto(1, "Test Admin", "admin@company.com", null, [role]));

    private async Task RegisterAndConfirmAsync()
    {
        IAuthService auth = Services.GetRequiredService<IAuthService>();
        MockEmailSender email = Services.GetRequiredService<MockEmailSender>();

        await auth.RegisterAsync(new RegisterRequest(
            "Nomsa Dlamini", "nomsa@company.com", "Portal!2026x", "Portal!2026x"));

        string token = MockEmailSender.TokenFrom(email.Sent[0])!;

        await auth.ConfirmEmailAsync(new ConfirmEmailRequest("nomsa@company.com", token));
    }
}
