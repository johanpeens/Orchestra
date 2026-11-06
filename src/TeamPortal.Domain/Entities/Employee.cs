using TeamPortal.Domain.Common;
using TeamPortal.Domain.Enums;
using TeamPortal.Domain.Identity;

namespace TeamPortal.Domain.Entities;

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

    /// <summary>
    /// Optional. Not every employee has a login - see AppUser, "Why AppUser and
    /// Employee are separate".
    /// </summary>
    public AppUser? User { get; set; }

    public ICollection<StepOwner> StepOwners { get; set; } = [];
}
