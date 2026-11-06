using TeamPortal.Domain.Enums;
using TeamPortal.Domain.Identity;

namespace TeamPortal.Domain.Entities;

public class ActivityLog
{
    /// <summary>long, not int - this table grows fastest.</summary>
    public long Id { get; set; }

    /// <summary>Null for system actions, and set null when a user is removed.</summary>
    public int? UserId { get; set; }

    public AppUser? User { get; set; }

    public ActivityAction Action { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTime HappenedAt { get; set; } = DateTime.UtcNow;
}
