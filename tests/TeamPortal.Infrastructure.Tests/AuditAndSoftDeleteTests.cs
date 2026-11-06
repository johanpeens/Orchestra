using Microsoft.EntityFrameworkCore;
using TeamPortal.Domain.Entities;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Tests.Common;

namespace TeamPortal.Infrastructure.Tests;

public class AuditAndSoftDeleteTests
{
    private const int TestUserId = 42;

    [Fact]
    public async Task SaveChanges_OnAdd_StampsCreatedAtAndCreatedBy()
    {
        await using var fixture = new DatabaseFixture(TestUserId);

        await using (PortalDbContext db = fixture.NewContext())
        {
            db.Departments.Add(new Department { Name = "Sales", ColourHex = "#E55218" });
            await db.SaveChangesAsync();
        }

        await using PortalDbContext verify = fixture.NewContext();
        Department saved = await verify.Departments.SingleAsync();

        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        saved.CreatedByUserId.Should().Be(TestUserId);
        saved.UpdatedAt.Should().BeNull("nothing has changed it yet");
    }

    [Fact]
    public async Task SaveChanges_OnModify_StampsUpdatedAtAndUpdatedBy()
    {
        await using var fixture = new DatabaseFixture(TestUserId);
        int id;

        await using (PortalDbContext db = fixture.NewContext())
        {
            var department = new Department { Name = "Sales", ColourHex = "#E55218" };
            db.Departments.Add(department);
            await db.SaveChangesAsync();
            id = department.Id;
        }

        await using (PortalDbContext db = fixture.NewContext())
        {
            Department department = await db.Departments.SingleAsync(d => d.Id == id);
            department.Name = "New Business";
            await db.SaveChangesAsync();
        }

        await using PortalDbContext verify = fixture.NewContext();
        Department saved = await verify.Departments.SingleAsync(d => d.Id == id);

        saved.UpdatedAt.Should().NotBeNull();
        saved.UpdatedByUserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task SaveChanges_WithNobodySignedIn_LeavesTheUserColumnsNull()
    {
        // System writes - the seeder, a background job - have no user to blame.
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            db.Departments.Add(new Department { Name = "Sales", ColourHex = "#E55218" });
            await db.SaveChangesAsync();
        }

        await using PortalDbContext verify = fixture.NewContext();
        Department saved = await verify.Departments.SingleAsync();

        saved.CreatedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_OnRemove_SetsIsDeletedInsteadOfDeleting()
    {
        // Every Remove() becomes an UPDATE. A mistaken delete is one flag away
        // from being undone rather than gone.
        await using var fixture = new DatabaseFixture(TestUserId);
        int id;

        await using (PortalDbContext db = fixture.NewContext())
        {
            var department = new Department { Name = "Sales", ColourHex = "#E55218" };
            db.Departments.Add(department);
            await db.SaveChangesAsync();
            id = department.Id;

            db.Departments.Remove(department);
            await db.SaveChangesAsync();
        }

        await using PortalDbContext verify = fixture.NewContext();

        // IgnoreQueryFilters proves the row is still physically there.
        Department stillThere = await verify.Departments
            .IgnoreQueryFilters()
            .SingleAsync(d => d.Id == id);

        stillThere.IsDeleted.Should().BeTrue();
        stillThere.UpdatedByUserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task QueryFilter_SoftDeletedRows_AreHiddenFromNormalQueries()
    {
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            var department = new Department { Name = "Sales", ColourHex = "#E55218" };
            db.Departments.Add(department);
            await db.SaveChangesAsync();

            db.Departments.Remove(department);
            await db.SaveChangesAsync();
        }

        await using PortalDbContext verify = fixture.NewContext();

        (await verify.Departments.CountAsync()).Should().Be(0, "the filter hides it");
        (await verify.Departments.IgnoreQueryFilters().CountAsync()).Should().Be(1, "but it is still there");
    }

    [Fact]
    public async Task SoftDelete_FreesTheNameForReuse()
    {
        // The unique indexes are filtered on IsDeleted = 0, so a deleted
        // department does not permanently reserve its name.
        await using var fixture = new DatabaseFixture();

        await using (PortalDbContext db = fixture.NewContext())
        {
            var department = new Department { Name = "Sales", ColourHex = "#E55218" };
            db.Departments.Add(department);
            await db.SaveChangesAsync();

            db.Departments.Remove(department);
            await db.SaveChangesAsync();
        }

        await using PortalDbContext reuse = fixture.NewContext();
        reuse.Departments.Add(new Department { Name = "Sales", ColourHex = "#44B4A6" });

        Func<Task> act = async () => await reuse.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ActivityLog_IsNotSoftDeleted_BecauseItIsAppendOnly()
    {
        // ActivityLog does not inherit AuditableEntity - it is never edited or
        // deleted, so it needs neither the audit columns nor the filter.
        await using var fixture = new DatabaseFixture();
        using PortalDbContext db = fixture.NewContext();

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entity =
            db.Model.FindEntityType(typeof(ActivityLog));

        entity!.GetQueryFilter().Should().BeNull();
    }
}
