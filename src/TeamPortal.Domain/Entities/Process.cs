using TeamPortal.Domain.Common;

namespace TeamPortal.Domain.Entities;

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

    public ICollection<ProcessStep> Steps { get; set; } = [];
}
