using TeamPortal.Domain.Common;

namespace TeamPortal.Domain.Entities;

public class ProcessFolder : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<Process> Processes { get; set; } = [];
}
