using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace TeamPortal.Web.Tests;

public class AuthorisationSweepTests
{
    private static readonly Assembly Web = typeof(Program).Assembly;

    private static IEnumerable<Type> RoutablePages =>
        Web.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ComponentBase)))
            // GetCustomAttribute, singular, throws AmbiguousMatchException here:
            // ProcessPage carries three @page directives and therefore three
            // RouteAttributes. Any() is the form that survives a second route.
            .Where(t => t.GetCustomAttributes<RouteAttribute>().Any());

    [Fact]
    public void EveryPage_IsEitherProtectedOrDeliberatelyAnonymous()
    {
        // Not "every page has [Authorize]" - the login page cannot. The point is
        // that somebody decided, rather than a new page defaulting to open.
        IReadOnlyList<string> undecided =
        [
            .. RoutablePages
                .Where(t => !t.GetCustomAttributes<AuthorizeAttribute>().Any())
                .Where(t => !t.GetCustomAttributes<AllowAnonymousAttribute>().Any())
                .Select(t => t.Name)
        ];

        undecided.Should().BeEmpty(
            "a page with neither attribute is open to the internet by accident");
    }

    [Fact]
    public void TheAnonymousPages_AreOnlyTheOnesThatHaveToBe()
    {
        // A short, explicit list. Adding to it should take a conversation, and
        // this test is the conversation.
        // Every page in the sign-up and password-recovery flow, and nothing else.
        // Each one is reachable by someone with no account or no session, which
        // is the only reason to be on this list.
        string[] allowed =
        [
            "LoginPage", "ErrorPage", "NotFound",
            "RegisterPage", "ConfirmEmailPage", "ForgotPasswordPage", "ResetPasswordPage"
        ];

        IReadOnlyList<string> anonymous =
        [
            .. RoutablePages
                .Where(t => t.GetCustomAttributes<AllowAnonymousAttribute>().Any())
                .Select(t => t.Name)
                .Order(StringComparer.Ordinal)
        ];

        anonymous.Should().BeSubsetOf(allowed);
    }

    [Fact]
    public void TheProcessAndTeamPages_AreProtected()
    {
        // Named explicitly, because these two are the whole application and a
        // sweep that passed with zero pages found would be no help at all.
        RoutablePages.Should().Contain(t => t.Name == "ProcessPage");
        RoutablePages.Should().Contain(t => t.Name == "TeamPage");

        foreach (string name in new[] { "ProcessPage", "TeamPage" })
        {
            Type page = RoutablePages.Single(t => t.Name == name);

            page.GetCustomAttributes<AuthorizeAttribute>()
                .Should().NotBeEmpty($"{name} shows business data");
        }
    }

    [Fact]
    public void TheWebProject_DoesNotReferenceTheMockLayer()
    {
        // Phase B4 finished the swap. The mock is Part A scaffolding and must not
        // be able to creep back into a running build.
        IReadOnlyList<string> mockReferences =
        [
            .. Web.GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .Where(n => n.Contains("Mock", StringComparison.OrdinalIgnoreCase))
        ];

        mockReferences.Should().BeEmpty();
    }
}
