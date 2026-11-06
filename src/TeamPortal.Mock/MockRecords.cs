using TeamPortal.Application.Enums;

namespace TeamPortal.Mock;

internal abstract class MockAuditable
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedByUserId { get; set; }

    /// <summary>
    /// Soft delete. Every read in every mock service filters this out, exactly as
    /// the EF Core global query filter will in Part B.
    /// </summary>
    public bool IsDeleted { get; set; }
}

internal sealed class MockDepartment : MockAuditable
{
    public string Name { get; set; } = string.Empty;
    public string ColourHex { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

internal sealed class MockEmployee : MockAuditable
{
    public string FullName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? PhotoUrl { get; set; }
    public StaffType StaffType { get; set; } = StaffType.Employee;
    public int DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
}

internal sealed class MockProcessFolder : MockAuditable
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

internal sealed class MockProcess : MockAuditable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProcessFolderId { get; set; }

    /// <summary>Position in the folder's flow strip. 1-based, no gaps.</summary>
    public int SortOrder { get; set; }

    /// <summary>The coloured tag on the process's card in the flow strip.</summary>
    public int? DepartmentId { get; set; }

    /// <summary>The status dot on the process's card in the flow strip.</summary>
    public int StepStatusId { get; set; } = 1;

    public bool IsArchived { get; set; }
}

internal sealed class MockStepStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColourHex { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
}

internal sealed class MockProcessStep : MockAuditable
{
    public int ProcessId { get; set; }
    public int StepNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DepartmentId { get; set; }
    public int StepStatusId { get; set; }
}

/// <summary>Join row. Composite key, hard-deleted - it has no IsDeleted flag.</summary>
internal sealed class MockStepOwner
{
    public int ProcessStepId { get; set; }
    public int EmployeeId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public bool IsPrimary { get; set; }
}

internal sealed class MockStepImage
{
    public int Id { get; set; }
    public int ProcessStepId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int? UploadedByUserId { get; set; }
}

/// <summary>Append-only. Never edited, never deleted.</summary>
internal sealed class MockActivityLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public ActivityAction Action { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime HappenedAt { get; set; } = DateTime.UtcNow;
}

internal sealed class MockNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A login account. Part A stores the password in clear text on purpose - there is
/// nothing real to protect, and a fake hash would only pretend at security.
/// Phase B3 replaces this entirely with ASP.NET Core Identity.
/// </summary>
internal sealed class MockUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int? EmployeeId { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public List<string> Roles { get; set; } = [];

    /// <summary>Seeded accounts are confirmed; registered ones start false.</summary>
    public bool EmailConfirmed { get; set; } = true;

    /// <summary>
    /// The confirmation token, kept only so the mock can behave like Identity.
    /// The real implementation never stores one - Identity derives it from the
    /// security stamp, which is why using it invalidates it.
    /// </summary>
    public string? EmailConfirmationToken { get; set; }

    /// <summary>Approved by default; only self-registration sets it Pending.</summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;

    public DateTime? ApprovalDecidedAt { get; set; }

    public int? ApprovalDecidedByUserId { get; set; }

    public DateTime? RegisteredAt { get; set; }
}
