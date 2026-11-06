using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Domain.Identity;
using TeamPortal.Infrastructure;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Identity;
using TeamPortal.Mock;
using TeamPortal.Mock.Services;

namespace TeamPortal.Services.Tests.Common;

public sealed class SqlServiceFixture : ServiceFixture
{
    private readonly SqliteConnection _connection;

    public SqlServiceFixture()
    {
        // ":memory:" lives only as long as the connection is open, so hold it for
        // the life of the fixture.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Sqlite does not enforce foreign keys unless asked.
        using (SqliteCommand pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        Build();
    }

    public override string DemoPassword => DbSeeder.DefaultDemoPassword;

    /// <summary>Every command this fixture's contexts have sent.</summary>
    public CommandCounter Commands { get; } = new();

    /// <summary>Every email this fixture's services have sent.</summary>
    public MockEmailSender Email { get; } = new();

    /// <summary>The folder this fixture's uploads land in. One per fixture.</summary>
    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), "teamportal-tests", Guid.NewGuid().ToString("N"));

    protected override void Register(IServiceCollection services)
    {
        // Scoped for the same reason the app scopes it - see AddSqlData.
        services.AddDbContextFactory<PortalDbContext>(
            options => options
                .UseSqlite(_connection)
                // Counts the commands that actually reach the database, for the
                // N+1 test. One counter per fixture, never a static - xUnit runs
                // test classes in parallel, and a shared counter picks up another
                // test's queries.
                .AddInterceptors(Commands),
            lifetime: ServiceLifetime.Scoped);

        // The same registrations Program.cs uses in Part B. Nothing about the
        // services themselves knows it is talking to SQLite.
        services.AddSqlServices();

        // Real Identity - the same registrations Program.cs uses, so a test can
        // never pass under password or lockout rules the app does not have.
        // This also supplies ICurrentUserService and IAuthService.
        services.AddLogging();
        services.AddPortalIdentityCore();
        services.AddSingleton<IClaimsPrincipalAccessor, NoClaimsPrincipalAccessor>();

        // SignInManager asks for the scheme provider even when it never signs
        // anyone in through a cookie.
        services.AddAuthentication();

        // Throws rather than shrugs. This used to be a no-op, and that no-op hid
        // a real bug for a whole phase: IdentityAuthService called
        // SignInManager.SignInAsync, which appends a Set-Cookie header, and in a
        // live Blazor circuit that throws "Headers are read-only, response has
        // already started". The tests passed; every real sign-in failed.
        //
        // A stub that is more forgiving than production is not a stub, it is a
        // blindfold. This one behaves the way the circuit does.
        services.AddSingleton<IAuthenticationService, ResponseAlreadyStartedAuthenticationService>();

        // A plain holder, NOT the framework's HttpContextAccessor. That one keeps
        // the context in an AsyncLocal, which does not survive the await
        // boundaries in a test - SignInManager then throws "HttpContext must not
        // be null" somewhere in the middle of a flow that was working.
        //
        // RequestServices points at this scope because HttpContext.SignInAsync
        // resolves IAuthenticationService from there, which routes it to the
        // no-op above.
        services.AddScoped<IHttpContextAccessor>(sp => new HeldHttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { RequestServices = sp }
        });

        // Nothing leaves the building in a test. The mock sender keeps every
        // message so a test can read the confirmation link out of it and follow
        // it - which is what a real user does, without a mailbox in the loop.
        services.AddSingleton(Email);
        services.AddSingleton<IEmailSender>(Email);
        services.AddSingleton<IAppUrlProvider>(new MockAppUrlProvider());

        // Phase B4 - the real store, over a throwaway folder this fixture owns
        // and deletes on the way out.
        services.AddLocalDiskFileStore(StorageRoot);
    }

    protected override void Prepare()
    {
        IDbContextFactory<PortalDbContext> factory = Get<IDbContextFactory<PortalDbContext>>();

        using PortalDbContext db = factory.CreateDbContext();
        db.Database.EnsureCreated();

        // Blocking on purpose. A constructor cannot await, and this is an
        // in-memory database with no synchronisation context to deadlock against.
        DbSeeder.SeedAsync(
            db,
            Get<UserManager<AppUser>>(),
            Get<RoleManager<AppRole>>())
            .GetAwaiter().GetResult();

        // Signed in as an administrator by default. Phase B5 made every write
        // path check a role, and the contract tests are about what the service
        // does for someone who is allowed to do it. Who is refused is a separate
        // question, asked by AuthorisationTests.
        SignInAs(1, AppRoles.Admin);
    }

    /// <summary>
    /// The signed-in user, for tests that need an identity without going through
    /// the password. Sets the same state a real sign-in would.
    /// </summary>
    public override void SignInAs(int userId, params string[] roles) =>
        Get<CurrentUserState>().SetUser(
            new CurrentUserDto(userId, "Test User", "test@company.com", null, roles));

    /// <summary>Nobody signed in, for the tests that ask what happens then.</summary>
    public void SignOut() => Get<CurrentUserState>().SetUser(null);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        _connection.Dispose();

        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }
}

/// <summary>
/// Stands in for the cookie handler as it behaves inside a Blazor circuit: the
/// response has already been sent, so anything that tries to write a header
/// throws. Nothing in the service layer may sign in or out - see AccountEndpoints
/// for where that is allowed to happen.
/// </summary>
public sealed class ResponseAlreadyStartedAuthenticationService : IAuthenticationService
{
    private const string Message =
        "Headers are read-only, response has already started. " +
        "Sign-in and sign-out must go through AccountEndpoints, not the service layer.";

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(
        HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task ForbidAsync(
        HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignInAsync(
        HttpContext context, string? scheme, ClaimsPrincipal principal,
        AuthenticationProperties? properties) => throw new InvalidOperationException(Message);

    public Task SignOutAsync(
        HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        throw new InvalidOperationException(Message);
}



/// <summary>
/// Counts the commands EF Core actually sends. Cheaper and more honest than
/// reasoning about the LINQ - it reports what reached the database.
/// </summary>
public sealed class CommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>
/// Holds one HttpContext in a field. See the note where this is registered for
/// why the framework's own accessor cannot be used here.
/// </summary>
public sealed class HeldHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
