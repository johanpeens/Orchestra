using TeamPortal.Domain.Common;

namespace TeamPortal.Domain.Entities;

public class Department : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string ColourHex { get; set; } = "#4A2767";

    public int SortOrder { get; set; }

    public ICollection<Employee> Employees { get; set; } = [];

    public ICollection<ProcessStep> ProcessSteps { get; set; } = [];

    /// <summary>
    /// Processes tagged with this department - the "Select Business Unit"
    /// relationship. Not in the original design sketch in Frontend/DataModel.md,
    /// added here for the reverse navigation Process.Department needs.
    /// </summary>
    public ICollection<Process> Processes { get; set; } = [];
}
