using TeamPortal.Application.Common;
using TeamPortal.Application.Enums;

namespace TeamPortal.Mock;

internal sealed class MockDataStore
{
    private int _departmentId;
    private int _employeeId;
    private int _processFolderId;
    private int _processId;
    private int _processStepId;
    private int _stepImageId;
    private int _notificationId;
    private int _userId;
    private long _activityLogId;

    public MockDataStore() => Seed();

    /// <summary>
    /// Every read and every write takes this lock. A Blazor Server app runs
    /// concurrent circuits against one store, and List&lt;T&gt; is not thread-safe.
    /// </summary>
    public object Gate { get; } = new();

    public List<MockDepartment> Departments { get; } = [];
    public List<MockEmployee> Employees { get; } = [];
    public List<MockProcessFolder> ProcessFolders { get; } = [];
    public List<MockProcess> Processes { get; } = [];
    public List<MockStepStatus> StepStatuses { get; } = [];
    public List<MockProcessStep> ProcessSteps { get; } = [];
    public List<MockStepOwner> StepOwners { get; } = [];
    public List<MockStepImage> StepImages { get; } = [];
    public List<MockActivityLog> ActivityLogs { get; } = [];
    public List<MockNotification> Notifications { get; } = [];
    public List<MockUser> Users { get; } = [];

    // --- Id generation -------------------------------------------------------

    public int NextDepartmentId() => ++_departmentId;
    public int NextEmployeeId() => ++_employeeId;
    public int NextProcessFolderId() => ++_processFolderId;
    public int NextProcessId() => ++_processId;
    public int NextProcessStepId() => ++_processStepId;
    public int NextStepImageId() => ++_stepImageId;
    public int NextNotificationId() => ++_notificationId;
    public int NextUserId() => ++_userId;
    public long NextActivityLogId() => ++_activityLogId;

    // --- Helpers used by every service ---------------------------------------

    /// <summary>Live departments only, mirroring the Part B query filter.</summary>
    public IEnumerable<MockDepartment> LiveDepartments => Departments.Where(d => !d.IsDeleted);

    public IEnumerable<MockEmployee> LiveEmployees => Employees.Where(e => !e.IsDeleted);

    public IEnumerable<MockProcessFolder> LiveFolders => ProcessFolders.Where(f => !f.IsDeleted);

    public IEnumerable<MockProcess> LiveProcesses => Processes.Where(p => !p.IsDeleted);

    public IEnumerable<MockProcessStep> LiveSteps => ProcessSteps.Where(s => !s.IsDeleted);

    /// <summary>
    /// Appends an activity row. Called by every write path, which is what makes
    /// "View Activity Log" and "View Process History" show anything.
    /// </summary>
    public void WriteActivity(
        ActivityAction action, string entityName, int? entityId, string summary, int? userId = null)
    {
        ActivityLogs.Add(new MockActivityLog
        {
            Id = NextActivityLogId(),
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Summary = summary,
            HappenedAt = DateTime.UtcNow
        });
    }

    // --- Seed ----------------------------------------------------------------

    /// <summary>
    /// Seeded from the seven screenshots in docs/design/. Phase B1's DbSeeder must
    /// produce the same data, so the swap is invisible on screen.
    /// </summary>
    private void Seed()
    {
        SeedStepStatuses();
        SeedDepartments();
        SeedEmployees();
        SeedUsers();
        SeedProcesses();
    }

    private void SeedStepStatuses()
    {
        StepStatuses.AddRange(
        [
            new MockStepStatus { Id = 1, Name = "Not Started", ColourHex = BrandPalette.Neutral40, SortOrder = 1, IsDefault = true },
            new MockStepStatus { Id = 2, Name = "In Progress", ColourHex = BrandPalette.Orange100, SortOrder = 2 },
            new MockStepStatus { Id = 3, Name = "Complete", ColourHex = BrandPalette.Aqua100, SortOrder = 3 },
            new MockStepStatus { Id = 4, Name = "Blocked", ColourHex = BrandPalette.Red100, SortOrder = 4 }
        ]);
    }

    private void SeedDepartments()
    {
        // One list, used in two places: the filter column on the Team screen AND
        // the "Select Business Unit" choice on the Add Process modal. Adding a
        // department on the Team screen therefore shows up in the process
        // selector straight away - there is only ever one source.
        (string Name, string Colour)[] seed =
        [
            ("Sales", BrandPalette.Orange100),
            ("Accounts", BrandPalette.Gold100),
            ("Underwriting", BrandPalette.Purple80),
            ("Management", BrandPalette.Purple100),
            ("Corporate", BrandPalette.Neutral60),
            ("Business Enablement", BrandPalette.Aqua100),
            ("Risk & Compliance", BrandPalette.Red100)
        ];

        for (int i = 0; i < seed.Length; i++)
        {
            Departments.Add(new MockDepartment
            {
                Id = NextDepartmentId(),
                Name = seed[i].Name,
                ColourHex = seed[i].Colour,
                SortOrder = i + 1
            });
        }
    }

    private void SeedEmployees()
    {
        // The eight cards on screen 05, all in Business Enablement.
        int enablement = Departments.First(d => d.Name == "Business Enablement").Id;

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
            Employees.Add(new MockEmployee
            {
                Id = NextEmployeeId(),
                FullName = name,
                JobTitle = title,
                Email = $"{name.Split(' ')[0].ToLowerInvariant()}@company.com",
                StaffType = type,
                DepartmentId = enablement
            });
        }
    }

    private void SeedUsers()
    {
        // Part A sign-ins. One account per role so the UI can be checked as each.
        // Passwords are clear text and deliberately obvious - see MockUser.
        (string Email, string Name, string Role, string? EmployeeName)[] seed =
        [
            ("admin@company.com", "Admin User", AppRoles.Admin, null),
            ("sarah@company.com", "Sarah", AppRoles.Manager, "Sarah"),
            ("maria@company.com", "Maria", AppRoles.Member, "Maria"),
            ("viewer@company.com", "Viewer User", AppRoles.Viewer, null)
        ];

        foreach ((string email, string name, string role, string? employeeName) in seed)
        {
            Users.Add(new MockUser
            {
                Id = NextUserId(),
                Email = email,
                Password = DemoPassword,
                FullName = name,
                Roles = [role],
                EmployeeId = employeeName is null
                    ? null
                    : Employees.FirstOrDefault(e => e.FullName == employeeName)?.Id
            });
        }
    }

    /// <summary>
    /// The password every seeded account uses in Part A. Not a secret, and not a
    /// credential - there is no real data behind it. Phase B3 deletes this and
    /// seeds the first admin from user secrets instead.
    /// </summary>
    public const string DemoPassword = "Portal!2026";

    private void SeedProcesses()
    {
        MockProcessFolder business = new()
        {
            Id = NextProcessFolderId(),
            Name = "The Business",
            SortOrder = 1
        };

        // The original designs used a different folder name. It is called
        // Client Onboarding here.
        MockProcessFolder onboarding = new()
        {
            Id = NextProcessFolderId(),
            Name = "Client Onboarding",
            SortOrder = 2
        };

        ProcessFolders.AddRange([business, onboarding]);

        // The seven cards in the flow strip on screen 02 are PROCESSES. Each one
        // holds its own steps, and Next past the last step of a process moves on
        // to the first step of the next process.
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

            Processes.Add(new MockProcess
            {
                Id = NextProcessId(),
                Name = name,
                Description = description,
                ProcessFolderId = onboarding.Id,
                DepartmentId = Departments.First(d => d.Name == department).Id,
                StepStatusId = statusId,
                SortOrder = i + 1
            });
        }

        SeedStepsForFirstProcess();
    }

    /// <summary>
    /// Only the first process is filled in, mirroring the design: screen 02 shows
    /// step 1 of "Initial Contact" with a description and four responsible people,
    /// and the rest of the strip is still to be written up.
    /// </summary>
    private void SeedStepsForFirstProcess()
    {
        MockProcess initialContact = Processes.First(p => p.Name == "Initial Contact");

        (string Name, string? Description)[] steps =
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
            (string name, string? description) = steps[i];

            ProcessSteps.Add(new MockProcessStep
            {
                Id = NextProcessStepId(),
                ProcessId = initialContact.Id,
                StepNumber = i + 1,
                Name = name,
                Description = description,
                DepartmentId = initialContact.DepartmentId,
                StepStatusId = i == 0 ? 2 : 1
            });
        }

        MockProcessStep first = ProcessSteps.First(s => s.ProcessId == initialContact.Id && s.StepNumber == 1);

        foreach (string owner in new[] { "Sarah", "Maria", "James", "David Chen" })
        {
            StepOwners.Add(new MockStepOwner
            {
                ProcessStepId = first.Id,
                EmployeeId = Employees.First(e => e.FullName == owner).Id,
                IsPrimary = owner == "Sarah"
            });
        }
    }
}
