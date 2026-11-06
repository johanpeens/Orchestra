using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Common;
using TeamPortal.Domain.Entities;
using TeamPortal.Domain.Enums;
using TeamPortal.Domain.Identity;

namespace TeamPortal.Infrastructure.Data;

public static class DbSeeder
{
    /// <summary>
    /// The password every seeded demo account uses. Read from configuration in
    /// SIT and above - never hard-code a real credential. See the overload that
    /// takes one.
    /// </summary>
    public const string DefaultDemoPassword = "Portal!2026";

    public static async Task SeedAsync(
        PortalDbContext db,
        UserManager<AppUser>? userManager = null,
        RoleManager<AppRole>? roleManager = null,
        string demoPassword = DefaultDemoPassword,
        SeedAdminOptions? admin = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await SeedStepStatusesAsync(db, ct);
        await SeedDepartmentsAsync(db, ct);
        await SeedEmployeesAsync(db, ct);
        await SeedProcessesAsync(db, ct);

        if (roleManager is not null)
        {
            await SeedRolesAsync(roleManager);
        }

        if (userManager is not null)
        {
            await SeedUsersAsync(db, userManager, demoPassword);

            if (admin is { IsConfigured: true })
            {
                await SeedConfiguredAdminAsync(userManager, admin);
            }
        }
    }

    /// <summary>
    /// The real administrator, from configuration. Idempotent like everything
    /// else here: if the account already exists it is left alone, so a restart
    /// does not quietly reset a password somebody has since changed.
    /// </summary>
    private static async Task SeedConfiguredAdminAsync(
        UserManager<AppUser> userManager, SeedAdminOptions options)
    {
        if (await userManager.FindByEmailAsync(options.Email) is not null)
        {
            return;
        }

        var user = new AppUser
        {
            UserName = options.Email,
            Email = options.Email,
            FullName = options.FullName ?? options.Email,

            // Both gates already open. This account is not self-registered -
            // whoever put the password in the secret store is the approval.
            EmailConfirmed = true,
            ApprovalStatus = ApprovalStatus.Approved,
            ApprovalDecidedAt = DateTime.UtcNow,
            IsActive = true
        };

        IdentityResult created = await userManager.CreateAsync(user, options.Password);

        if (created.Succeeded)
        {
            await userManager.AddToRoleAsync(user, options.Role);
        }
    }

    private static async Task SeedStepStatusesAsync(PortalDbContext db, CancellationToken ct)
    {
        if (await db.StepStatuses.AnyAsync(ct))
        {
            return;
        }

        db.StepStatuses.AddRange(
            new StepStatus { Id = 1, Name = "Not Started", ColourHex = BrandPalette.Neutral40, SortOrder = 1, IsDefault = true },
            new StepStatus { Id = 2, Name = "In Progress", ColourHex = BrandPalette.Orange100, SortOrder = 2 },
            new StepStatus { Id = 3, Name = "Complete", ColourHex = BrandPalette.Aqua100, SortOrder = 3 },
            new StepStatus { Id = 4, Name = "Blocked", ColourHex = BrandPalette.Red100, SortOrder = 4 });

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedDepartmentsAsync(PortalDbContext db, CancellationToken ct)
    {
        if (await db.Departments.AnyAsync(ct))
        {
            return;
        }

        // One list, three surfaces: the Team screen filter, the Add Process
        // modal's "Select Business Unit" tabs, and the colour tags on cards.
        // Every colour here MUST be one of BrandPalette.DepartmentSwatches.
        // Underwriting was Purple80 and Corporate was Neutral60, and neither is
        // a swatch - so opening Edit Department on either showed no colour
        // selected, and the tag on the flow strip was a colour nobody could
        // choose. SeededDepartments_UseOnlyColoursThePickerOffers now fails if
        // that happens again.
        (string Name, string Colour)[] seed =
        [
            ("Sales", BrandPalette.Orange100),
            ("Accounts", BrandPalette.Gold100),
            ("Underwriting", BrandPalette.SwatchBlue),
            ("Management", BrandPalette.Purple100),
            ("Corporate", BrandPalette.SwatchGreen),
            ("Business Enablement", BrandPalette.Aqua100),
            ("Risk & Compliance", BrandPalette.Red100)
        ];

        for (int i = 0; i < seed.Length; i++)
        {
            db.Departments.Add(new Department
            {
                Name = seed[i].Name,
                ColourHex = seed[i].Colour,
                SortOrder = i + 1
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedEmployeesAsync(PortalDbContext db, CancellationToken ct)
    {
        if (await db.Employees.AnyAsync(ct))
        {
            return;
        }

        Department enablement =
            await db.Departments.SingleAsync(d => d.Name == "Business Enablement", ct);

        (string Name, string Title, StaffType Type)[] seed =
        [
            ("Sarah", "Project Manager", StaffType.Management),
            ("Maria", "Business Analyst", StaffType.Employee),
            ("James", "Solutions Architect", StaffType.Employee),
            ("David Chen", "Solutions Developer", StaffType.Employee),
            ("Alex", "BI Developer", StaffType.Employee),
            ("Priya", "UX/Software Engineer", StaffType.Employee),
            ("Lena", "Business Analyst", StaffType.Employee),
            ("Marcus", "Software Engineer / UX", StaffType.Employee)
        ];

        foreach ((string name, string title, StaffType type) in seed)
        {
            db.Employees.Add(new Employee
            {
                FullName = name,
                JobTitle = title,
                Email = $"{name.Split(' ')[0].ToLowerInvariant()}@company.com",
                StaffType = type,
                DepartmentId = enablement.Id
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedProcessesAsync(PortalDbContext db, CancellationToken ct)
    {
        if (await db.ProcessFolders.AnyAsync(ct))
        {
            return;
        }

        var business = new ProcessFolder { Name = "The Business", SortOrder = 1 };
        var onboarding = new ProcessFolder { Name = "Client Onboarding", SortOrder = 2 };

        db.ProcessFolders.AddRange(business, onboarding);
        await db.SaveChangesAsync(ct);

        Dictionary<string, int> departments =
            await db.Departments.ToDictionaryAsync(d => d.Name, d => d.Id, ct);

        // The seven cards in the flow strip on screen 02 are PROCESSES. Each one
        // holds its own steps; Next past the last step of a process moves on to
        // the first step of the next.
        (string Name, string Department, int StatusId, string Description)[] processes =
        [
            ("Initial Contact", "Sales", 2,
                "Receive and qualify new client inquiries."),
            ("Gather Info", "Sales", 3,
                "Collect the documents and details needed to assess the client."),
            ("Review & Assess", "Risk & Compliance", 1,
                "Check the client against risk and compliance requirements."),
            ("Prepare Proposal", "Underwriting", 1,
                "Put together the proposal and pricing for the client."),
            ("Client Approval", "Sales", 1,
                "Walk the client through the proposal and capture their approval."),
            ("Onboard Client", "Accounts", 1,
                "Set the client up on the systems and confirm their details."),
            ("Handover & Kick-off", "Management", 1,
                "Hand the client over to their account manager and start delivery.")
        ];

        for (int i = 0; i < processes.Length; i++)
        {
            (string name, string department, int statusId, string description) = processes[i];

            db.Processes.Add(new Process
            {
                Name = name,
                Description = description,
                ProcessFolderId = onboarding.Id,
                DepartmentId = departments[department],
                StepStatusId = statusId,
                SortOrder = i + 1
            });
        }

        await db.SaveChangesAsync(ct);
        await SeedStepsForFirstProcessAsync(db, ct);
    }

    /// <summary>
    /// Only the first process is filled in, mirroring the design: screen 02 shows
    /// step 1 of "Initial Contact" with a description and four responsible people,
    /// and the rest of the strip is still to be written up.
    /// </summary>
    private static async Task SeedStepsForFirstProcessAsync(PortalDbContext db, CancellationToken ct)
    {
        Process initialContact =
            await db.Processes.SingleAsync(p => p.Name == "Initial Contact", ct);

        (string Name, string Description)[] steps =
        [
            ("Receive the inquiry",
                "The initial contact step involves receiving and qualifying new client " +
                "inquiries. The team verifies basic information, creates a lead record, " +
                "and ensures the client receives a prompt acknowledgment."),
            ("Verify basic information", "Check the details supplied against what is on file."),
            ("Create the lead record", "Capture the client on the system as a new lead."),
            ("Acknowledge the client", "Send the client confirmation that we have their inquiry.")
        ];

        for (int i = 0; i < steps.Length; i++)
        {
            (string name, string description) = steps[i];

            db.ProcessSteps.Add(new ProcessStep
            {
                ProcessId = initialContact.Id,
                StepNumber = i + 1,
                Name = name,
                Description = description,
                DepartmentId = initialContact.DepartmentId,
                StepStatusId = i == 0 ? 2 : 1
            });
        }

        await db.SaveChangesAsync(ct);

        ProcessStep first = await db.ProcessSteps
            .SingleAsync(s => s.ProcessId == initialContact.Id && s.StepNumber == 1, ct);

        string[] owners = ["Sarah", "Maria", "James", "David Chen"];

        foreach (string owner in owners)
        {
            Employee employee = await db.Employees.SingleAsync(e => e.FullName == owner, ct);

            db.StepOwners.Add(new StepOwner
            {
                ProcessStepId = first.Id,
                EmployeeId = employee.Id,
                IsPrimary = owner == "Sarah"
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedRolesAsync(RoleManager<AppRole> roleManager)
    {
        foreach (string role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new AppRole { Name = role });
            }
        }
    }

    private static async Task SeedUsersAsync(
        PortalDbContext db, UserManager<AppUser> userManager, string demoPassword)
    {
        (string Email, string Name, string Role, string? EmployeeName)[] seed =
        [
            ("admin@company.com", "Admin User", AppRoles.Admin, null),
            ("sarah@company.com", "Sarah", AppRoles.Manager, "Sarah"),
            ("maria@company.com", "Maria", AppRoles.Member, "Maria"),
            ("viewer@company.com", "Viewer User", AppRoles.Viewer, null)
        ];

        foreach ((string email, string name, string role, string? employeeName) in seed)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            int? employeeId = employeeName is null
                ? null
                : (await db.Employees.FirstOrDefaultAsync(e => e.FullName == employeeName))?.Id;

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = name,
                EmployeeId = employeeId
            };

            IdentityResult created = await userManager.CreateAsync(user, demoPassword);

            if (created.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
