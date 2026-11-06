namespace TeamPortal.Domain.Entities;

public class StepStatus
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColourHex { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>Exactly one row must be true. Used for brand new steps and processes.</summary>
    public bool IsDefault { get; set; }

    public ICollection<Process> Processes { get; set; } = [];

    public ICollection<ProcessStep> ProcessSteps { get; set; } = [];
}
