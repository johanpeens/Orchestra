using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeamPortal.Domain.Identity;
using TeamPortal.Infrastructure.Identity;

namespace TeamPortal.Web.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Anonymous by necessity - the whole point is that the caller is not
        // signed in yet. The ticket is the authorisation, and it only exists
        // because a password was already checked in the circuit.
        endpoints.MapGet("/account/complete-sign-in", async (
                [FromQuery] string? ticket,
                [FromQuery] string? returnUrl,
                [FromServices] ISignInTicketStore tickets,
                [FromServices] SignInManager<AppUser> signIn,
                [FromServices] UserManager<AppUser> users) =>
            {
                SignInTicket? redeemed = tickets.Redeem(ticket ?? string.Empty);

                if (redeemed is null)
                {
                    // Unknown, expired or already used. Back to the form; there is
                    // nothing useful or safe to say about which.
                    return Results.Redirect("/login");
                }

                AppUser? user = await users.FindByIdAsync(
                    redeemed.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture));

                if (user is null)
                {
                    return Results.Redirect("/login");
                }

                // Here it works: this is a real request with a response nobody has
                // started writing yet.
                await signIn.SignInAsync(user, redeemed.IsPersistent);

                return Results.Redirect(SafeReturnUrl(returnUrl));
            })
            .AllowAnonymous()
            .WithName("CompleteSignIn");

        endpoints.MapGet("/account/sign-out", async (
                [FromServices] SignInManager<AppUser> signIn) =>
            {
                await signIn.SignOutAsync();

                return Results.Redirect("/login");
            })
            // Signing out when you are already signed out is not an error, so this
            // does not demand an identity.
            .AllowAnonymous()
            .WithName("SignOut");

        return endpoints;
    }

    /// <summary>
    /// Only ever a path on this site. An attacker who can put
    /// ?returnUrl=https://evil.example on a link must not be able to bounce a
    /// freshly signed-in user off-site - the same check the login page makes,
    /// repeated here because this endpoint can be reached without it.
    /// </summary>
    private static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            return "/";
        }

        // "//evil.example" parses as relative but leaves the site.
        return returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/";
    }
}
