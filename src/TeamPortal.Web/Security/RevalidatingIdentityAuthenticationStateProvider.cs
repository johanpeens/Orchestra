using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using TeamPortal.Domain.Identity;

namespace TeamPortal.Web.Security;

internal sealed class RevalidatingIdentityAuthenticationStateProvider
    : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RevalidatingIdentityAuthenticationStateProvider(
        ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
        : base(loggerFactory) => _scopeFactory = scopeFactory;

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticationState);

        // Its own scope: this runs on a timer, outside any circuit's scope, and
        // a UserManager resolved from the circuit would outlive its context.
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        AppUser? user = await users.GetUserAsync(authenticationState.User);

        if (user is null || !user.IsActive)
        {
            return false;
        }

        // The security stamp changes whenever the password or roles change, so
        // comparing it is how a stale circuit finds out.
        if (users.SupportsUserSecurityStamp)
        {
            string? stamp = authenticationState.User.FindFirstValue(
                users.Options.ClaimsIdentity.SecurityStampClaimType);

            return stamp == await users.GetSecurityStampAsync(user);
        }

        return true;
    }
}
