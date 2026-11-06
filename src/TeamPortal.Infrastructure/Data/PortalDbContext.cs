using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Abstractions;
using TeamPortal.Domain.Common;
using TeamPortal.Domain.Entities;
using TeamPortal.Domain.Identity;

namespace TeamPortal.Infrastructure.Data;

public class PortalDbContext
    : IdentityDbContext<AppUser, AppRole, int,
                        AppUserClaim, AppUserRole, AppUserLogin,
                        AppRoleClaim, AppUserToken>
{
    private readonly ICurrentUserService? _currentUser;

    /// <summary>
    /// One constructor, deliberately. Two of them - an options-only overload plus
    /// this - reads as a convenience, but both accept a DbContextOptions, so
    /// AddDbContextFactory cannot tell which to call and throws "Multiple
    /// constructors accepting all given argument types have been found".
    ///
    /// The user is nullable because there is not always one: `dotnet ef` builds a
    /// context at design time with nobody signed in, and so does the seeder.
    /// A null user leaves the audit columns null, which is what a system write is.
    /// </summary>
    public PortalDbContext(
        DbContextOptions<PortalDbContext> options, ICurrentUserService? currentUser)
        : base(options) => _currentUser = currentUser;

    // --- Organisation --------------------------------------------------------
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();

    // --- Processes -----------------------------------------------------------
    public DbSet<ProcessFolder> ProcessFolders => Set<ProcessFolder>();
    public DbSet<Process> Processes => Set<Process>();
    public DbSet<ProcessStep> ProcessSteps => Set<ProcessStep>();
    public DbSet<StepStatus> StepStatuses => Set<StepStatus>();
    public DbSet<StepOwner> StepOwners => Set<StepOwner>();
    public DbSet<StepImage> StepImages => Set<StepImage>();

    // --- Support -------------------------------------------------------------
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Must run first - it configures the Identity tables.
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(PortalDbContext).Assembly);

        // Rename Identity's defaults from AspNetUsers to AppUser, etc.
        builder.Entity<AppUser>().ToTable("AppUser");
        builder.Entity<AppRole>().ToTable("AppRole");
        builder.Entity<AppUserRole>().ToTable("AppUserRole");
        builder.Entity<AppUserClaim>().ToTable("AppUserClaim");
        builder.Entity<AppUserLogin>().ToTable("AppUserLogin");
        builder.Entity<AppUserToken>().ToTable("AppUserToken");
        builder.Entity<AppRoleClaim>().ToTable("AppRoleClaim");

        // Identity's own string columns have no length by default, which would
        // make them nvarchar(max). Model_EveryStringProperty_HasAMaxLength
        // enforces that none escape.
        builder.Entity<AppUser>(b =>
        {
            b.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            b.Property(x => x.PhotoUrl).HasMaxLength(500);
            b.Property(x => x.PasswordHash).HasMaxLength(256);
            b.Property(x => x.SecurityStamp).HasMaxLength(256);
            b.Property(x => x.ConcurrencyStamp).HasMaxLength(256);
            b.Property(x => x.PhoneNumber).HasMaxLength(32);

            // Stored as int, like every other enum here - a rename must not
            // orphan rows. See Frontend/DataModel.md, Enums.
            b.Property(x => x.ApprovalStatus).HasConversion<int>();

            // The admin grid filters on this constantly, and it is the query
            // behind the "waiting" badge on every page load for an admin.
            b.HasIndex(x => new { x.ApprovalStatus, x.EmailConfirmed })
             .HasDatabaseName("IX_AppUser_ApprovalStatus_EmailConfirmed");

            // Who decided. Restrict, not Cascade: removing an administrator must
            // not delete the accounts they approved.
            b.HasOne<AppUser>()
             .WithMany()
             .HasForeignKey(x => x.ApprovalDecidedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Employee)
             .WithOne(e => e.User)
             .HasForeignKey<AppUser>(x => x.EmployeeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppRole>(b =>
        {
            b.Property(x => x.Description).HasMaxLength(250);
            b.Property(x => x.ConcurrencyStamp).HasMaxLength(256);
        });

        builder.Entity<AppUserRole>(b =>
        {
            b.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
            b.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);
        });

        SetIdentityStringLengths(builder);

        // Hide soft-deleted rows from every query, everywhere. Day-to-day code
        // never has to remember .Where(x => !x.IsDeleted).
        builder.Entity<Department>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Employee>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ProcessFolder>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Process>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ProcessStep>().HasQueryFilter(x => !x.IsDeleted);

        // StepOwner and StepImage carry no IsDeleted of their own - they are hard
        // deleted with their step. They still need a filter, because they sit on
        // the required end of a relationship with a filtered entity: without one,
        // EF warns (10622) and a soft-deleted step leaves its owners and images
        // visible to any query that reaches them from the other direction.
        builder.Entity<StepOwner>().HasQueryFilter(x => !x.ProcessStep.IsDeleted);
        builder.Entity<StepImage>().HasQueryFilter(x => !x.ProcessStep.IsDeleted);
    }

    /// <summary>
    /// Identity declares several string keys and token columns with no length.
    /// Left alone they become nvarchar(max), which cannot be indexed.
    /// </summary>
    private static void SetIdentityStringLengths(ModelBuilder builder)
    {
        builder.Entity<AppUserLogin>(b =>
        {
            b.Property(x => x.LoginProvider).HasMaxLength(128);
            b.Property(x => x.ProviderKey).HasMaxLength(128);
            b.Property(x => x.ProviderDisplayName).HasMaxLength(128);
        });

        builder.Entity<AppUserToken>(b =>
        {
            b.Property(x => x.LoginProvider).HasMaxLength(128);
            b.Property(x => x.Name).HasMaxLength(128);
            b.Property(x => x.Value).HasMaxLength(512);
        });

        builder.Entity<AppUserClaim>(b =>
        {
            b.Property(x => x.ClaimType).HasMaxLength(256);
            b.Property(x => x.ClaimValue).HasMaxLength(512);
        });

        builder.Entity<AppRoleClaim>(b =>
        {
            b.Property(x => x.ClaimType).HasMaxLength(256);
            b.Property(x => x.ClaimValue).HasMaxLength(512);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditColumns();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditColumns();
        return base.SaveChanges();
    }

    /// <summary>
    /// Stamps CreatedAt/UpdatedAt and turns every hard delete into a soft one,
    /// so no caller has to remember either. See Frontend/DataModel.md,
    /// "Stamping the audit columns automatically".
    /// </summary>
    private void StampAuditColumns()
    {
        int? userId = _currentUser?.UserId;
        DateTime now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedByUserId = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedByUserId = userId;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedByUserId = userId;
                    break;

                default:
                    break;
            }
        }
    }
}
