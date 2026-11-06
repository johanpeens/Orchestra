namespace TeamPortal.Application.Dtos;

public sealed record FolderContentsDto(
    int FolderId,
    string Name,
    int ProcessCount,
    int StepCount)
{
    public bool IsEmpty => ProcessCount == 0 && StepCount == 0;

    /// <summary>e.g. "7 processes and 12 steps". Reads in a sentence.</summary>
    public string Summary =>
        IsEmpty
            ? "It is empty."
            : $"{Plural(ProcessCount, "process", "processes")} and {Plural(StepCount, "step", "steps")}";

    private static string Plural(int count, string one, string many) =>
        count == 1 ? $"1 {one}" : $"{count} {many}";
}
