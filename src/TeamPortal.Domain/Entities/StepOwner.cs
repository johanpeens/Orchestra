namespace TeamPortal.Domain.Entities;

/// <summary>
/// An explicit join entity rather than a plain ICollection&lt;Employee&gt;
/// because it carries its own data (AssignedAt, IsPrimary). Does NOT inherit
/// AuditableEntity - it has a composite key and is hard-deleted when an
/// assignment is removed, never soft-deleted.
/// </summary>
public class StepOwner
{
    public int ProcessStepId { get; set; }

    public ProcessStep ProcessStep { get; set; } = null!;

    public int EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The one name shown when space is tight.</summary>
    public bool IsPrimary { get; set; }
}
