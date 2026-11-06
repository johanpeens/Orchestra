using Microsoft.AspNetCore.Identity;
using TeamPortal.Domain.Entities;
using TeamPortal.Domain.Enums;

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
    // -------------------------------------------------------------------------

    public string FullName { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// The second gate. Confirming an email proves somebody can read that
    /// mailbox; approval is a person deciding they belong here.
    ///
    /// Defaults to Approved, not Pending: accounts an administrator creates
    /// directly, and the seeded demo accounts, are approved by the act of being
    /// created. Only self-registration sets this to Pending.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;

    public DateTime? ApprovalDecidedAt { get; set; }

    /// <summary>Who approved or rejected them. Null while still pending.</summary>
    public int? ApprovalDecidedByUserId { get; set; }

    /// <summary>When they registered. Null for accounts that were never self-registered.</summary>
    public DateTime? RegisteredAt { get; set; }

    /// <summary>
    /// Optional link to the Employee card shown on the Team screen. Not every
    /// employee needs a login, and not every login is an employee - see
    /// Frontend/DataModel.md, "Why AppUser and Employee are separate".
    /// </summary>
    public int? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = [];

    public ICollection<ActivityLog> ActivityLogs { get; set; } = [];

    public ICollection<Notification> Notifications { get; set; } = [];
}
