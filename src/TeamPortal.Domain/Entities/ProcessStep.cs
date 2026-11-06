using TeamPortal.Domain.Common;

namespace TeamPortal.Domain.Entities;

public class ProcessStep : AuditableEntity
{
    /// <summary>
    /// Unique per process. The service layer renumbers the survivors after every
    /// insert or delete so there are never gaps or duplicates - see
    /// ProcessStepService.ResequenceAsync in Frontend/DataModel.md.
    /// </summary>
    public int StepNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int ProcessId { get; set; }

    public Process Process { get; set; } = null!;

    public int? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public int StepStatusId { get; set; } = 1;

    public StepStatus StepStatus { get; set; } = null!;

    public ICollection<StepOwner> Owners { get; set; } = [];

    public ICollection<StepImage> Images { get; set; } = [];
}
