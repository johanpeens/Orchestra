using Microsoft.EntityFrameworkCore;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Tests.Common;

namespace TeamPortal.Infrastructure.Tests;

public class ModelTests
{
    [Fact]
    public async Task Model_EveryEntity_HasAPrimaryKey()
    {
        await using var fixture = new DatabaseFixture();
        using PortalDbContext db = fixture.NewContext();

        IReadOnlyList<string> keyless =
        [
            .. db.Model.GetEntityTypes()
                .Where(t => t.FindPrimaryKey() is null)
                .Select(t => t.ShortName())
        ];

        keyless.Should().BeEmpty("every table needs a primary key");
    }

    [Fact]
    public async Task Model_EveryStringProperty_HasAMaxLength()
    {
        // Without an explicit length a string column becomes nvarchar(max),
        // which cannot be indexed and bloats every row.
        await using var fixture = new DatabaseFixture();
        using PortalDbContext db = fixture.NewContext();

        IReadOnlyList<string> unbounded =
        [
            .. db.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is null)
                .Select(p => $"{p.DeclaringType.ShortName()}.{p.Name}")
        ];

        unbounded.Should().BeEmpty("every string column needs an explicit length");
    }

    [Fact]
    public async Task Model_EveryEnum_IsStoredAsAnInt()
    {
        // Storing an enum as its name means a rename silently orphans every row.
        await using var fixture = new DatabaseFixture();
        using PortalDbContext db = fixture.NewContext();

        IReadOnlyList<string> notInt =
        [
            .. db.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType.IsEnum || Nullable.GetUnderlyingType(p.ClrType)?.IsEnum == true)
                .Where(p => p.GetProviderClrType() == typeof(string))
                .Select(p => $"{p.DeclaringType.ShortName()}.{p.Name}")
        ];

        notInt.Should().BeEmpty("enums are stored as int - see Frontend/DataModel.md, Enums");
    }

    [Fact]
    public async Task Model_TheIdentityTables_AreRenamedFromAspNetDefaults()
    {
        await using var fixture = new DatabaseFixture();
        using PortalDbContext db = fixture.NewContext();

        IReadOnlyList<string?> tables =
        [
            .. db.Model.GetEntityTypes().Select(t => t.GetTableName())
        ];

        tables.Should().Contain("AppUser").And.Contain("AppRole");
        tables.Should().NotContain("AspNetUsers", "the Identity defaults are renamed");
    }

    [Fact]
    public async Task Model_HasEveryExpectedTable()
    {
        await using var fixture = new DatabaseFixture();
        using PortalDbContext db = fixture.NewContext();

        IReadOnlyList<string?> tables =
        [
            .. db.Model.GetEntityTypes().Select(t => t.GetTableName())
        ];

        // Ten domain tables plus the seven Identity ones.
        tables.Should().Contain([
            "Department", "Employee", "ProcessFolder", "Process", "StepStatus",
            "ProcessStep", "StepOwner", "StepImage", "ActivityLog", "Notification",
            "AppUser", "AppRole", "AppUserRole", "AppUserClaim", "AppRoleClaim",
            "AppUserLogin", "AppUserToken"
        ]);
    }

    [Fact]
    public async Task Model_BusinessUnit_IsGoneEntirely()
    {
        // Dropped at phase A12 once no screen used it. This test is here so it
        // cannot creep back in without someone deciding to put it back.
        await using var fixture = new DatabaseFixture();
        using PortalDbContext db = fixture.NewContext();

        db.Model.GetEntityTypes().Select(t => t.GetTableName())
            .Should().NotContain("BusinessUnit");
    }
}
