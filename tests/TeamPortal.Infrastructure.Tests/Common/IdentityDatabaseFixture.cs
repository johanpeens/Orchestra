using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Domain.Identity;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Identity;

namespace TeamPortal.Infrastructure.Tests.Common;

public sealed class IdentityDatabaseFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public IdentityDatabaseFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (SqliteCommand pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContextFactory<PortalDbContext>(
            options => options.UseSqlite(_connection),
            lifetime: ServiceLifetime.Scoped);

        // The same registrations the app uses, so a test can never pass under
        // password rules the running app does not have.
        services.AddPortalIdentityCore();
        services.AddSingleton<IClaimsPrincipalAccessor, NoClaimsPrincipalAccessor>();
        services.AddScoped<ICurrentUserService, NobodySignedIn>();

        _root = services.BuildServiceProvider(validateScopes: true);
        _scope = _root.CreateScope();

        using PortalDbContext db = NewContext();
        db.Database.EnsureCreated();
    }

    public PortalDbContext NewContext() =>
        _scope.ServiceProvider.GetRequiredService<IDbContextFactory<PortalDbContext>>()
            .CreateDbContext();

    public async Task SeedAsync(SeedAdminOptions? admin)
    {
        await using PortalDbContext db = NewContext();

        await DbSeeder.SeedAsync(
            db,
            _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            _scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>(),
            DbSeeder.DefaultDemoPassword,
            admin);
    }

    public async Task<IList<string>> RolesForAsync(string email)
    {
        UserManager<AppUser> users = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        AppUser user = (await users.FindByEmailAsync(email))!;

        return await users.GetRolesAsync(user);
    }

    public async Task<string> PasswordHashForAsync(string email)
    {
        await using PortalDbContext db = NewContext();

        return (await db.Users.AsNoTracking().SingleAsync(u => u.Email == email)).PasswordHash!;
    }

    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await _root.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>The seeder runs as nobody. That is what a system write is.</summary>
    private sealed class NobodySignedIn : ICurrentUserService
    {
        public CurrentUserDto? User => null;

        public int? UserId => null;

        public bool IsSignedIn => false;

        public bool IsInRole(string role) => false;
    }
}
