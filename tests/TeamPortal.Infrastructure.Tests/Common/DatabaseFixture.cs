using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Infrastructure.Data;

namespace TeamPortal.Infrastructure.Tests.Common;

public sealed class DatabaseFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<PortalDbContext> _options;

    public DatabaseFixture(int? currentUserId = null)
    {
        // ":memory:" lives only as long as the connection is open, so hold it.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Sqlite does not enforce foreign keys unless asked.
        using (SqliteCommand pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        _options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        CurrentUser = new StubCurrentUser(currentUserId);

        using PortalDbContext db = NewContext();
        db.Database.EnsureCreated();
    }

    public ICurrentUserService CurrentUser { get; }

    /// <summary>
    /// A fresh context over the same database. Reading through a new context is
    /// what proves a write actually reached the database rather than sitting in
    /// the change tracker.
    /// </summary>
    public PortalDbContext NewContext() => new(_options, CurrentUser);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private sealed class StubCurrentUser(int? userId) : ICurrentUserService
    {
        public CurrentUserDto? User { get; } =
            userId is null ? null : new CurrentUserDto(userId.Value, "Test User", "test@company.com", null, []);

        public int? UserId => userId;

        public bool IsSignedIn => userId is not null;

        public bool IsInRole(string role) => false;
    }
}
