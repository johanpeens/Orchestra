```markdown Frontend\DataModel.md
# Data Model & C# Entities

## Purpose

This document is the **single source of truth** for the database design of the Team
Portal app. It describes every C# model (entity class), how the models link to each
other, and how EF Core maps them to SQL Server tables.

Two companion diagrams live next to this file:

- `docs/developer/ErDiagram.html` - the EF Core entity relationship map.
- `docs/developer/UserFlow.html` - the screen and modal flow.

---

# The App in Plain English

The portal does two jobs.

**Job 1 - Keep a list of people.**
The business is split into **Business Units** (Operations, Finance, Legal, HR).
Each business unit contains **Departments** (Sales, Accounts, Underwriting,
Management, Corporate, Business Enablement). Each department contains **Employees**
(Sarah the Project Manager, Maria the Business Analyst, and so on).

**Job 2 - Keep a list of how work gets done.**
Work is written down at three levels:

1. A **Process Folder** is a heading in the sidebar, like "Client Onboarding".
2. A **Process** is one stage of that work, like "Initial Contact" or "Gather Info".
   The folder's processes run in order and are the cards in the flow strip across
   the top of screen 02. Each carries a department tag and a **Status** dot.
3. A **Process Step** is one thing somebody actually does inside a process, like
   "Verify basic information". Steps run in order within their process and are the
   numbered list on the left of screen 02. Each step can have **Step Owners** (the
   people responsible) and **Step Images** (uploaded reference screenshots).

The Back and Next buttons walk **steps**, and they cross process boundaries: press
Next on the last step of "Initial Contact" and you land on the first step of
"Gather Info". That is what lets someone read a whole folder end to end without
touching the flow strip.

Everything a person does is written to an **Activity Log** so the "View Activity Log"
and "View Process History" buttons have something to show.

Logging in is handled by **ASP.NET Core Identity**, which brings its own set of
tables for users, roles and passwords.

---

# Naming & Design Conventions

These rules apply to every model in this document.

| Rule | Decision |
|------|----------|
| Table names | Singular PascalCase (`Employee`, not `Employees`) |
| DbSet names | Plural PascalCase (`public DbSet<Employee> Employees`) |
| Primary key | `int Id`, `IDENTITY(1,1)`, auto-increment |
| Foreign key | `<EntityName>Id` (e.g. `DepartmentId`) |
| Booleans | Start with `Is` or `Has` (`IsActive`, `IsDeleted`) |
| Dates | `DateTime` stored as UTC, suffix `At` or `On` (`CreatedAt`) |
| Text | `string` with an explicit `MaxLength`; never `nvarchar(max)` unless noted |
| Colours | 7-character hex string, always uppercase (`#4A2767`) |
| Deletes | **Soft delete** on business data (`IsDeleted` flag), hard delete on link rows |
| Nulls | Nullable reference types are ON; `string?` means "optional" |

## Why `int` keys and not `Guid`

`int` keys are smaller, read better in URLs (`/process/12` instead of a 36-character
blob), and index faster. There is no need to merge databases or generate keys offline,
which are the two reasons you would reach for `Guid`. Identity is therefore configured
as `IdentityUser<int>` rather than the default `IdentityUser<string>`.

---

# Shared Base Class

Every business table carries the same six audit columns. Rather than repeating them,
all entities inherit from `AuditableEntity`.

```csharp
// *****************************************************************************
// Purpose : Base class giving every table its Id and audit trail columns.
// Ticket  : SD-PENDING
// *****************************************************************************

namespace TeamPortal.Domain.Common;

public abstract class AuditableEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }

    /// <summary>Soft delete. Rows are hidden, never physically removed.</summary>
    public bool IsDeleted { get; set; }
}
```

A global query filter hides soft-deleted rows automatically, so day-to-day queries
never have to remember `.Where(x => !x.IsDeleted)`.

---

# Enums

Enums are stored as `int` in the database. The explicit numbers matter: never renumber
an existing value, only append new ones.

```csharp
namespace TeamPortal.Domain.Enums;

/// <summary>Is this person a normal employee or a manager?</summary>
public enum StaffType
{
    Employee   = 1,
    Management = 2
}

/// <summary>What happened, for the activity log.</summary>
public enum ActivityAction
{
    Created   = 1,
    Updated   = 2,
    Deleted   = 3,
    Viewed    = 4,
    LoggedIn  = 5,
    LoggedOut = 6,
    Uploaded  = 7
}
```

`StepStatus` is deliberately **not** an enum. It is a lookup table, because the
business will want to add statuses and change their colours without a code deploy.

---

# Part 1 - Identity Core Models

ASP.NET Core Identity ships with seven tables. We subclass all of them so the key type
is `int` and so we can add our own columns later without a painful migration.

## AppUser

The login account. One row per person who can sign in.

```csharp
// *****************************************************************************
// Purpose : Login account. Extends ASP.NET Identity with portal-specific fields.
// Ticket  : SD-PENDING
// *****************************************************************************

using Microsoft.AspNetCore.Identity;

namespace TeamPortal.Domain.Identity;

public class AppUser : IdentityUser<int>
{
    // --- Inherited from IdentityUser<int> -----------------------------------
    // int      Id
    // string?  UserName
    // string?  NormalizedUserName
    // string?  Email
    // string?  NormalizedEmail
    // bool     EmailConfirmed
    // string?  PasswordHash
    // string?  SecurityStamp
    // string?  ConcurrencyStamp
    // string?  PhoneNumber
    // bool     PhoneNumberConfirmed
    // bool     TwoFactorEnabled
    // DateTimeOffset? LockoutEnd
    // bool     LockoutEnabled
    // int      AccessFailedCount
    // ------------------------------------------------------------------------

    public string FullName { get; set; } = string.Empty;   // max 150
    public string? PhotoUrl { get; set; }                  // max 500
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Optional link to the Employee card shown on the Team screen.</summary>
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
```

### Why AppUser and Employee are separate

Not every employee needs a login (a contractor might appear on the team page but never
sign in), and not every login is an employee (a system or integration account). Keeping
them apart avoids a nullable password column on the Employee table. The optional
`EmployeeId` link joins them when both exist.

## AppRole

```csharp
public class AppRole : IdentityRole<int>
{
    public string? Description { get; set; }   // max 250

    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
}
```

### Seeded roles

| Role      | Can do |
|-----------|--------|
| `Admin`   | Everything, including adding users and editing business units |
| `Manager` | Create/edit processes, steps, employees and departments |
| `Member`  | View everything, upload step screenshots |
| `Viewer`  | Read-only |

## The remaining five Identity tables

These exist only so the key type is `int`. They gain no extra columns.

```csharp
public class AppUserRole : IdentityUserRole<int>
{
    public AppUser User { get; set; } = null!;
    public AppRole Role { get; set; } = null!;
}

public class AppUserClaim : IdentityUserClaim<int> { }
public class AppUserLogin : IdentityUserLogin<int> { }
public class AppUserToken : IdentityUserToken<int> { }
public class AppRoleClaim : IdentityRoleClaim<int> { }
```

## Identity table map

| C# class       | SQL table         | Holds |
|----------------|-------------------|-------|
| `AppUser`      | `AppUser`         | Accounts, password hashes, lockout state |
| `AppRole`      | `AppRole`         | Role names |
| `AppUserRole`  | `AppUserRole`     | Which user has which role |
| `AppUserClaim` | `AppUserClaim`    | Extra permissions per user |
| `AppRoleClaim` | `AppRoleClaim`    | Extra permissions per role |
| `AppUserLogin` | `AppUserLogin`    | External logins (Microsoft/Google) - future use |
| `AppUserToken` | `AppUserToken`    | Two-factor and password-reset tokens |

## Identity options

```csharp
builder.Services
    .AddIdentity<AppUser, AppRole>(options =>
    {
        options.Password.RequiredLength         = 10;
        options.Password.RequireDigit           = true;
        options.Password.RequireUppercase       = true;
        options.Password.RequireLowercase       = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);

        options.User.RequireUniqueEmail         = true;
        options.SignIn.RequireConfirmedAccount  = false;   // phase 1
    })
    .AddEntityFrameworkStores<PortalDbContext>()
    .AddDefaultTokenProviders();
```

The login screen's **Remember me** checkbox maps to
`SignInManager.PasswordSignInAsync(email, password, isPersistent: rememberMe, lockoutOnFailure: true)`.
**Forgot password?** uses `UserManager.GeneratePasswordResetTokenAsync`.

---

# Part 2 - Organisation Models

## BusinessUnit

**Dropped at phase A12.** A `BusinessUnit` table was drafted here and an
`AddBusinessUnitDialog` was built for it, but by A9 nothing on any screen read or
wrote it: the Add Process modal's "Select Business Unit" tabs turned out to mean
`Department`, and the sidebar that would have listed business units was removed.

A screen with no way to reach it is not a feature, so the decision made at A12 was
to delete the table, its service, its DTOs and its dialog rather than carry a
column nothing populates into Part B. If a grouping above departments is ever
needed, add it back deliberately, with a screen that uses it from the start.

## Department

The organisation's units - Sales, Accounts, Underwriting, Management, Corporate,
Business Enablement, Risk & Compliance.

**There is one list, and it is used in three places.** The Team screen filters by
it, the Add Process modal offers it as "Select Business Unit", and the colour
picked in the Edit Department modal becomes the tag on process and step cards.

That is deliberate: adding a unit on the Team screen makes it selectable on the
Add Process screen immediately, with no second list to keep in step. Any future
change that copies this list, caches it, or lets a process pick from somewhere
else breaks that guarantee.

| Property         | Type      | Rules | Notes |
|------------------|-----------|-------|-------|
| `Id`             | `int`     | PK | |
| `Name`           | `string`  | Required, max 100, **unique** | "Business Enablement" |
| `ColourHex`      | `string`  | Required, exactly 7 chars, regex `^#[0-9A-F]{6}$` | Default `#4A2767` |
| `SortOrder`      | `int`     | Default 0 | |
| *audit columns*  | | | |

```csharp
public class Department : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string ColourHex { get; set; } = "#4A2767";
    public int SortOrder { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<ProcessStep> ProcessSteps { get; set; } = new List<ProcessStep>();
}
```

> The `(7)`, `(0)` counts beside each department on the Team screen are **not** stored.
> They are computed with `Departments.Select(d => new { d, Count = d.Employees.Count })`.
> Never store a count you can calculate - it will drift out of date.

## Employee

One card on the Team screen.

| Property        | Type        | Rules | Notes |
|-----------------|-------------|-------|-------|
| `Id`            | `int`       | PK | |
| `FullName`      | `string`    | Required, max 150 | "Sarah" / "David Chen" |
| `JobTitle`      | `string?`   | Max 150 | "Project Manager" |
| `Email`         | `string?`   | Max 256, unique when present | |
| `PhotoUrl`      | `string?`   | Max 500 | Null means show the initials avatar |
| `StaffType`     | `StaffType` | Required, default `Employee` | The Employee/Management radio |
| `DepartmentId`  | `int`       | FK, required | |
| `IsActive`      | `bool`      | Default `true` | Leavers stay for history |
| *audit columns* | | | |

```csharp
public class Employee : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? PhotoUrl { get; set; }
    public StaffType StaffType { get; set; } = StaffType.Employee;
    public bool IsActive { get; set; } = true;

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public AppUser? User { get; set; }
    public ICollection<StepOwner> StepOwners { get; set; } = new List<StepOwner>();
}
```

---

# Part 3 - Process Models

## ProcessFolder

The grey folder rows in the left sidebar ("The Business 9", "Client Onboarding 12"). It is
just a container so long lists stay navigable.

| Property        | Type     | Rules | Notes |
|-----------------|----------|-------|-------|
| `Id`            | `int`    | PK | |
| `Name`          | `string` | Required, max 100, unique | |
| `SortOrder`     | `int`    | Default 0 | |
| *audit columns* | | | |

```csharp
public class ProcessFolder : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<Process> Processes { get; set; } = new List<Process>();
}
```

## Process

One stage of a folder's work, e.g. "Initial Contact". A folder's processes run in
`SortOrder` and are the cards in the flow strip on screen 02, so a process carries
the department tag and status dot that card shows.

| Property          | Type      | Rules | Notes |
|-------------------|-----------|-------|-------|
| `Id`              | `int`     | PK | |
| `Name`            | `string`  | Required, max 150 | Unique within its folder |
| `Description`     | `string?` | Max 1000 | Shown under the page title |
| `ProcessFolderId` | `int`     | FK, required | |
| `DepartmentId`    | `int?`    | FK, optional | The coloured tag on its flow-strip card - also the "Select Business Unit" choice on the Add Process modal |
| `StepStatusId`    | `int`     | FK, required, default 1 | The status dot on its flow-strip card |
| `SortOrder`       | `int`     | Required, **unique per folder** | Position in the flow strip, 1..n |
| `IsArchived`      | `bool`    | Default `false` | Hidden from the sidebar, kept for history |
| *audit columns*   | | | |

```csharp
public class Process : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>The coloured tag on this process's card in the flow strip.</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>The status dot on this process's card in the flow strip.</summary>
    public int StepStatusId { get; set; } = 1;
    public StepStatus StepStatus { get; set; } = null!;

    public int ProcessFolderId { get; set; }
    public ProcessFolder ProcessFolder { get; set; } = null!;

    public ICollection<ProcessStep> Steps { get; set; } = new List<ProcessStep>();
}
```

## StepStatus

The coloured dot on each step card. A lookup table, not an enum, so the business can
add "On Hold" without a deploy.

| Property    | Type     | Rules | Notes |
|-------------|----------|-------|-------|
| `Id`        | `int`    | PK | |
| `Name`      | `string` | Required, max 50, unique | |
| `ColourHex` | `string` | Required, 7 chars | |
| `SortOrder` | `int`    | Default 0 | |
| `IsDefault` | `bool`   | Exactly one row must be `true` | Used for brand new steps |

### Seeded rows

| Id | Name        | ColourHex | Meaning |
|----|-------------|-----------|---------|
| 1  | Not Started | `#7F7F85` | Grey dot |
| 2  | In Progress | `#E55218` | Orange dot |
| 3  | Complete    | `#44B4A6` | Aqua dot |
| 4  | Blocked     | `#CF1230` | Red dot |

## ProcessStep

One numbered row in the step list - a single thing a person does inside a process.

| Property        | Type      | Rules | Notes |
|-----------------|-----------|-------|-------|
| `Id`            | `int`     | PK | |
| `ProcessId`     | `int`     | FK, required | |
| `StepNumber`    | `int`     | Required, **unique per process** | The 1..7 badge |
| `Name`          | `string`  | Required, max 150 | "Initial Contact" |
| `Description`   | `string?` | Max 2000 | The "Step Description" panel |
| `DepartmentId`  | `int?`    | FK, optional | The coloured tag under the name |
| `StepStatusId`  | `int`     | FK, required, default 1 | |
| *audit columns* | | | |

```csharp
public class ProcessStep : AuditableEntity
{
    public int StepNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int ProcessId { get; set; }
    public Process Process { get; set; } = null!;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int StepStatusId { get; set; } = 1;
    public StepStatus StepStatus { get; set; } = null!;

    public ICollection<StepOwner> Owners { get; set; } = new List<StepOwner>();
    public ICollection<StepImage> Images { get; set; } = new List<StepImage>();
}
```

### Renumbering rule

`StepNumber` is unique per process. When a step is inserted or deleted, the service
layer must renumber the remaining steps in one transaction so there are never gaps or
duplicates. Do this in `ProcessStepService.ResequenceAsync(processId)`.

## StepOwner

The join table behind "Responsible Persons". A step can have many people; a person can
own many steps.

| Property        | Type       | Rules |
|-----------------|------------|-------|
| `ProcessStepId` | `int`      | PK part 1, FK |
| `EmployeeId`    | `int`      | PK part 2, FK |
| `AssignedAt`    | `DateTime` | Default UTC now |
| `IsPrimary`     | `bool`     | Default `false` - the one name shown when space is tight |

```csharp
public class StepOwner
{
    public int ProcessStepId { get; set; }
    public ProcessStep ProcessStep { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public bool IsPrimary { get; set; }
}
```

This is an explicit join entity rather than a plain `ICollection<Employee>` because it
carries its own data (`AssignedAt`, `IsPrimary`). It does **not** inherit
`AuditableEntity` - it has a composite key and is hard-deleted when an assignment is
removed.

## StepImage

An uploaded reference screenshot. The Add Step modal allows PNG, JPG or WEBP up to 5 MB.

| Property           | Type       | Rules | Notes |
|--------------------|------------|-------|-------|
| `Id`               | `int`      | PK | |
| `ProcessStepId`    | `int`      | FK, required | |
| `FileName`         | `string`   | Required, max 260 | Original name, for the download prompt |
| `StoredPath`       | `string`   | Required, max 500 | Relative path or blob key - never a full disk path |
| `ContentType`      | `string`   | Required, max 100 | Whitelist: `image/png`, `image/jpeg`, `image/webp` |
| `SizeBytes`        | `long`     | Required, max 5 242 880 | |
| `SortOrder`        | `int`      | Default 0 | |
| `UploadedAt`       | `DateTime` | Default UTC now | |
| `UploadedByUserId` | `int?`     | FK to `AppUser` | |

```csharp
public class StepImage
{
    public int Id { get; set; }

    public int ProcessStepId { get; set; }
    public ProcessStep ProcessStep { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int? UploadedByUserId { get; set; }
    public AppUser? UploadedByUser { get; set; }
}
```

> **Never store the image bytes in the database.** Save the file to disk or blob
> storage and keep only the path. A table full of 5 MB blobs makes every backup,
> restore and query slow.

---

# Part 4 - Support Models

## ActivityLog

Feeds "View Activity Log" and "View Process History". Append-only: rows are written,
never edited or deleted.

| Property     | Type             | Rules | Notes |
|--------------|------------------|-------|-------|
| `Id`         | `long`           | PK | `long` because this table grows fastest |
| `UserId`     | `int?`           | FK to `AppUser` | Null for system actions |
| `Action`     | `ActivityAction` | Required | |
| `EntityName` | `string`         | Required, max 100 | `"Process"`, `"Employee"` |
| `EntityId`   | `int?`           | | Which row was touched |
| `Summary`    | `string`         | Required, max 500 | "Added step 'Client Approval'" |
| `HappenedAt` | `DateTime`       | Default UTC now, **indexed descending** | |

```csharp
public class ActivityLog
{
    public long Id { get; set; }

    public int? UserId { get; set; }
    public AppUser? User { get; set; }

    public ActivityAction Action { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime HappenedAt { get; set; } = DateTime.UtcNow;
}
```

## Notification

Backs the bell icon in the top bar.

| Property    | Type       | Rules | Notes |
|-------------|------------|-------|-------|
| `Id`        | `int`      | PK | |
| `UserId`    | `int`      | FK, required | Who sees it |
| `Title`     | `string`   | Required, max 150 | |
| `Message`   | `string?`  | Max 500 | |
| `LinkUrl`   | `string?`  | Max 500 | Where clicking takes you |
| `IsRead`    | `bool`     | Default `false` | |
| `CreatedAt` | `DateTime` | Default UTC now | |

---

# Relationship Summary

| From | To | Type | On delete | Why |
|------|----|------|-----------|-----|
| `Department` | `Employee` | 1 to many | `Restrict` | Move staff before deleting a dept |
| `Department` | `ProcessStep` | 1 to many | `Restrict` | |
| `Department` | `Process` | 1 to many | `Restrict` | The flow-strip tag |
| `StepStatus` | `Process` | 1 to many | `Restrict` | The flow-strip dot |
| `ProcessFolder` | `Process` | 1 to many | `Restrict` | Empty the folder first |
| `Process` | `ProcessStep` | 1 to many | `Cascade` | Steps are meaningless alone |
| `StepStatus` | `ProcessStep` | 1 to many | `Restrict` | Lookup rows are never deleted |
| `ProcessStep` | `StepOwner` | 1 to many | `Cascade` | |
| `Employee` | `StepOwner` | 1 to many | `Restrict` | Unassign before removing a person |
| `ProcessStep` | `StepImage` | 1 to many | `Cascade` | |
| `AppUser` | `Employee` | 1 to 0..1 | `Restrict` | |
| `AppUser` | `ActivityLog` | 1 to many | `SetNull` | Keep history if a user is removed |
| `AppUser` | `Notification` | 1 to many | `Cascade` | |
| `AppUser` | `AppRole` | many to many via `AppUserRole` | `Cascade` | Identity default |

`Restrict` is the default choice. `Cascade` is only used where the child row genuinely
cannot exist without its parent. Accidental cascades are the classic way to lose a
week of data with one delete.

---

# The DbContext

```csharp
// *****************************************************************************
// Purpose : EF Core database context for the Team Portal.
// Ticket  : SD-PENDING
// *****************************************************************************

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TeamPortal.Infrastructure.Data;

public class PortalDbContext
    : IdentityDbContext<AppUser, AppRole, int,
                        AppUserClaim, AppUserRole, AppUserLogin,
                        AppRoleClaim, AppUserToken>
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options)
        : base(options) { }

    // --- Organisation --------------------------------------------------------
    public DbSet<Department>   Departments   => Set<Department>();
    public DbSet<Employee>     Employees     => Set<Employee>();

    // --- Processes -----------------------------------------------------------
    public DbSet<ProcessFolder> ProcessFolders => Set<ProcessFolder>();
    public DbSet<Process>       Processes      => Set<Process>();
    public DbSet<ProcessStep>   ProcessSteps   => Set<ProcessStep>();
    public DbSet<StepStatus>    StepStatuses   => Set<StepStatus>();
    public DbSet<StepOwner>     StepOwners     => Set<StepOwner>();
    public DbSet<StepImage>     StepImages     => Set<StepImage>();

    // --- Support -------------------------------------------------------------
    public DbSet<ActivityLog>  ActivityLogs  => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // must run first - configures Identity

        // Pick up every IEntityTypeConfiguration<T> in this assembly.
        builder.ApplyConfigurationsFromAssembly(typeof(PortalDbContext).Assembly);

        // Rename the Identity tables from AspNetUsers to AppUser, etc.
        builder.Entity<AppUser>().ToTable("AppUser");
        builder.Entity<AppRole>().ToTable("AppRole");
        builder.Entity<AppUserRole>().ToTable("AppUserRole");
        builder.Entity<AppUserClaim>().ToTable("AppUserClaim");
        builder.Entity<AppUserLogin>().ToTable("AppUserLogin");
        builder.Entity<AppUserToken>().ToTable("AppUserToken");
        builder.Entity<AppRoleClaim>().ToTable("AppRoleClaim");

        // Hide soft-deleted rows from every query, everywhere.
        builder.Entity<Department>()   .HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Employee>()     .HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ProcessFolder>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Process>()      .HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ProcessStep>()  .HasQueryFilter(x => !x.IsDeleted);
    }
}
```

## Stamping the audit columns automatically

Override `SaveChangesAsync` so nobody has to remember to set `UpdatedAt`.

```csharp
public override Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var userId = _currentUser.UserId;   // injected ICurrentUserService

    foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
    {
        switch (entry.State)
        {
            case EntityState.Added:
                entry.Entity.CreatedAt       = DateTime.UtcNow;
                entry.Entity.CreatedByUserId = userId;
                break;

            case EntityState.Modified:
                entry.Entity.UpdatedAt       = DateTime.UtcNow;
                entry.Entity.UpdatedByUserId = userId;
                break;

            case EntityState.Deleted:
                // Turn every hard delete into a soft delete.
                entry.State                  = EntityState.Modified;
                entry.Entity.IsDeleted       = true;
                entry.Entity.UpdatedAt       = DateTime.UtcNow;
                entry.Entity.UpdatedByUserId = userId;
                break;
        }
    }

    return base.SaveChangesAsync(ct);
}
```

---

# Fluent API Configuration

One configuration class per entity, in `Infrastructure/Data/Configurations/`. Data
annotations are **not** used - keeping the rules out of the domain classes means the
domain project has no EF Core dependency.

```csharp
public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("Employee");
        b.HasKey(x => x.Id);

        b.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        b.Property(x => x.JobTitle).HasMaxLength(150);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.PhotoUrl).HasMaxLength(500);
        b.Property(x => x.StaffType).HasConversion<int>().IsRequired();

        b.HasOne(x => x.Department)
         .WithMany(d => d.Employees)
         .HasForeignKey(x => x.DepartmentId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Email)
         .IsUnique()
         .HasFilter("[Email] IS NOT NULL AND [IsDeleted] = 0");

        b.HasIndex(x => new { x.DepartmentId, x.IsActive });
    }
}
```

```csharp
public class ProcessStepConfiguration : IEntityTypeConfiguration<ProcessStep>
{
    public void Configure(EntityTypeBuilder<ProcessStep> b)
    {
        b.ToTable("ProcessStep");
        b.HasKey(x => x.Id);

        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(2000);

        b.HasOne(x => x.Process)
         .WithMany(p => p.Steps)
         .HasForeignKey(x => x.ProcessId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Department)
         .WithMany(d => d.ProcessSteps)
         .HasForeignKey(x => x.DepartmentId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.StepStatus)
         .WithMany(s => s.ProcessSteps)
         .HasForeignKey(x => x.StepStatusId)
         .OnDelete(DeleteBehavior.Restrict);

        // No two steps in one process may share a number.
        b.HasIndex(x => new { x.ProcessId, x.StepNumber })
         .IsUnique()
         .HasFilter("[IsDeleted] = 0");
    }
}
```

```csharp
public class StepOwnerConfiguration : IEntityTypeConfiguration<StepOwner>
{
    public void Configure(EntityTypeBuilder<StepOwner> b)
    {
        b.ToTable("StepOwner");

        // Composite key - one person can only be added to a step once.
        b.HasKey(x => new { x.ProcessStepId, x.EmployeeId });

        b.HasOne(x => x.ProcessStep)
         .WithMany(s => s.Owners)
         .HasForeignKey(x => x.ProcessStepId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Employee)
         .WithMany(e => e.StepOwners)
         .HasForeignKey(x => x.EmployeeId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

# Index Summary

| Table | Index | Unique | Reason |
|-------|-------|--------|--------|
| `Department` | `Name` | Yes | No duplicate departments |
| `Employee` | `Email` (filtered) | Yes | One account per address |
| `Employee` | `DepartmentId, IsActive` | No | Team screen filter |
| `ProcessFolder` | `Name` | Yes | |
| `Process` | `ProcessFolderId, Name` | Yes | |
| `Process` | `ProcessFolderId, SortOrder` | Yes | Flow-strip order, no gaps or ties |
| `ProcessStep` | `ProcessId, StepNumber` | Yes | No duplicate step numbers |
| `StepImage` | `ProcessStepId, SortOrder` | No | |
| `ActivityLog` | `HappenedAt DESC` | No | The log always reads newest first |
| `ActivityLog` | `EntityName, EntityId` | No | "History for this process" |
| `Notification` | `UserId, IsRead` | No | Unread badge count |

---

# Seed Data

Run once on first startup via `DbSeeder.SeedAsync(context, userManager, roleManager)`.
Seeding must be **idempotent** - safe to run on every boot.

1. **Step statuses** - the four rows listed above.
2. **Roles** - `Admin`, `Manager`, `Member`, `Viewer`.
3. **Business units** - Operations, Finance, Legal, HR.
4. **Departments** - Sales, Accounts, Underwriting, Management, Corporate,
   Business Enablement, each with a brand colour.
5. **Admin user** - email and password read from user secrets or environment
   variables. **Never** hard-code a password in the seeder.

---

# Migrations

```bash
dotnet ef migrations add InitialCreate --project TeamPortal.Infrastructure --startup-project TeamPortal.Web
```

```bash
dotnet ef database update --project TeamPortal.Infrastructure --startup-project TeamPortal.Web
```

Rules:

- One migration per pull request, named after what it does (`AddStepImageTable`).
- Read the generated `Up` and `Down` methods before committing. EF sometimes plans a
  drop-and-recreate that would lose data.
- Never edit a migration that has already run in SIT or above. Add a new one.
- Migrations are applied by the release pipeline, not by the app at startup, for
  anything above DEV.

---

# Screen to Model Map

| Screen / modal | Reads | Writes |
|----------------|-------|--------|
| `01-login-screen` | `AppUser` | `AppUser.LastLoginAt`, `ActivityLog` |
| `02-process-dashboard` | `ProcessFolder`, `Process` (the flow strip), `ProcessStep` (the step list), `StepStatus`, `Department`, `StepOwner`, `Employee`, `StepImage` | - |
| `03-add-process-modal` | `Department` | `Process`, `ActivityLog` |
| `04-add-step-modal` | `Department`, `Employee` | `ProcessStep`, `StepOwner`, `StepImage`, `ActivityLog` |
| `05-team-management-screen` | `Department`, `Employee` | - |
| `06-add-employee-modal` | `Department` | `Employee`, `ActivityLog` |
| `07-edit-department-modal` | `Department` | `Department`, `ActivityLog` |

---

# Validation Rules

Enforced in a FluentValidation validator per form model, and mirrored by the database
constraints above. Client-side validation is a convenience; the server is the authority.

## Add / Edit Process

- `Name` - required, 3 to 150 characters, unique within the folder.
- `Description` - optional, up to 1000 characters.
- `DepartmentId` - optional, must exist.

## Add / Edit Step

- `Name` - required, 3 to 150 characters.
- `Description` - optional, up to 2000 characters.
- `DepartmentId` - optional, must exist.
- `OwnerIds` - zero or more, each must be an active employee, no duplicates.
- `Screenshot` - optional; if present, content type in the whitelist and size
  under 5 MB. Validate the actual file signature, not just the extension.

## Add / Edit Employee

- `FullName` - required, 2 to 150 characters.
- `JobTitle` - optional, up to 150 characters.
- `DepartmentId` - required, must exist.
- `StaffType` - required, `Employee` or `Management`.
- `Photo` - optional, same image rules as above.

## Edit Department

- `Name` - required, 2 to 100 characters, unique.
- `ColourHex` - required, must match `^#[0-9A-F]{6}$`.

---

# Project Layout

```text
TeamPortal.Domain/
    Common/AuditableEntity.cs
    Enums/StaffType.cs
    Enums/ActivityAction.cs
    Identity/AppUser.cs
    Identity/AppRole.cs
    Identity/AppUserRole.cs
    Entities/Department.cs
    Entities/Employee.cs
    Entities/ProcessFolder.cs
    Entities/Process.cs
    Entities/ProcessStep.cs
    Entities/StepStatus.cs
    Entities/StepOwner.cs
    Entities/StepImage.cs
    Entities/ActivityLog.cs
    Entities/Notification.cs

TeamPortal.Infrastructure/
    Data/PortalDbContext.cs
    Data/Configurations/*.cs
    Data/DbSeeder.cs
    Migrations/

TeamPortal.Application/
    Dtos/
    Services/
    Validators/

TeamPortal.Web/            <- Blazor Server + MudBlazor, per Frontend/TeamConventions.md
```

## Entities never reach the UI

Blazor pages bind to DTOs, not entities. An entity carries navigation properties and
audit columns the page does not need, and binding straight to it invites accidental
over-posting. Map with an explicit projection:

```csharp
public record EmployeeCardDto(
    int Id,
    string FullName,
    string? JobTitle,
    string DepartmentName,
    string DepartmentColour,
    string? PhotoUrl,
    StaffType StaffType);
```

```csharp
var cards = await _db.Employees
    .Where(e => e.IsActive && e.DepartmentId == departmentId)
    .OrderBy(e => e.FullName)
    .Select(e => new EmployeeCardDto(
        e.Id, e.FullName, e.JobTitle,
        e.Department.Name, e.Department.ColourHex,
        e.PhotoUrl, e.StaffType))
    .AsNoTracking()
    .ToListAsync(ct);
```

`AsNoTracking()` on every read-only query. The change tracker is pure overhead when
nothing is being saved.

---

# Decisions Still Open

These need a business answer before the first migration is written.

1. **Multi-company?** The model assumes one company. Supporting several would add a
   `CompanyId` to `Department` and `ProcessFolder`, plus a tenant
   filter on every query. Far cheaper to decide now than to retrofit.
2. **Step approvals.** The flow shows Back/Next buttons. If a step must be *signed off*
   rather than just marked complete, that needs a `StepApproval` table
   (StepId, ApproverId, Decision, Comment, DecidedAt).
3. **File storage target.** Local disk in DEV is fine; SIT and above should use Azure
   Blob Storage. `StoredPath` is deliberately a plain string so either works.
4. **Activity log retention.** How many months before rows are archived?
5. **Does a process need its own owners?** Today only steps carry `StepOwner`. If a
   process needs an accountable person independent of its steps, that is a second
   join table, not a column.
6. **Employee vs user creation.** Does adding an employee also create a login, or are
   those two separate admin actions?

---

# Precedence

If this document conflicts with another Frontend document:

1. Explicit user instructions in the current chat
2. `Frontend/StartHere.md`
3. `Frontend/TeamConventions.md`
4. `Frontend/CI.md`
5. `Frontend/DataModel.md` (this file)
6. `Frontend/FontUsage.md`
```
