using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Domain.Identity;
using TeamPortal.Infrastructure.Data;

namespace TeamPortal.Infrastructure.Identity;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Everything sign-in needs except the cookie handler, which only the web
    /// project can wire because only it has a request pipeline. A test calls this
    /// and gets a working UserManager without pretending to be a web server.
    /// </summary>
    public static IServiceCollection AddPortalIdentityCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The password-reset token provider is a data protector underneath, so
        // this has to be here rather than left to the web host - otherwise a test
        // that resets a password fails on a missing IDataProtectionProvider.
        services.AddDataProtection();

        services.AddIdentityCore<AppUser>(ConfigureOptions)
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<PortalDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Scoped: one scope is one circuit, so two people signed in at once must
        // not see each other's identity.
        services.AddScoped<CurrentUserState>();
        services.AddScoped<ICurrentUserService, IdentityCurrentUserService>();
        services.AddScoped<IAuthService, IdentityAuthService>();

        // Singleton: the circuit that issues a ticket and the request that
        // redeems it are two different scopes, so anything narrower loses it.
        services.AddSingleton<ISignInTicketStore, InMemorySignInTicketStore>();
        services.AddScoped<IUserApprovalService, SqlUserApprovalService>();

        return services;
    }

    /// <summary>
    /// The password, lockout and email rules from Frontend/DataModel.md. Shared by
    /// the app and by the tests, so a test can never pass under rules the running
    /// app does not use.
    /// </summary>
    public static void ConfigureOptions(IdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Five attempts, then fifteen minutes. Matches the message the login
        // screen has been showing since phase A4.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = IdentityAuthService.MaxFailedAttempts;
        options.Lockout.DefaultLockoutTimeSpan = IdentityAuthService.LockoutDuration;

        options.User.RequireUniqueEmail = true;

        // Sign-in is by email, so the username is the email and has to allow
        // every character a valid address can contain.
        options.User.AllowedUserNameCharacters = string.Empty;

        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    }
}
