namespace TeamPortal.Domain.Common;

public abstract class AuditableEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedByUserId { get; set; }

    /// <summary>
    /// Soft delete. Rows are hidden by a global query filter (see
    /// PortalDbContext.OnModelCreating), never physically removed.
    /// </summary>
    public bool IsDeleted { get; set; }
}
