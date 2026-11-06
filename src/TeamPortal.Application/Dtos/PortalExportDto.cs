namespace TeamPortal.Application.Dtos;

public sealed record PortalExportDto(
    string GeneratedAtUtc,
    int FolderCount,
    int ProcessCount,
    int StepCount,
    int ScreenshotCount,
    IReadOnlyList<ExportFolderDto> Folders,
    IReadOnlyList<ExportDepartmentDto> Departments,
    IReadOnlyList<ExportStatusDto> Statuses)
{
    /// <summary>
    /// Everyone on the Team screen. The steps already name their owners, but as
    /// bare strings - "Sarah (Project Manager)" and nothing else. Without the
    /// roster, "who else does Sarah work with" or "who is in Underwriting" have
    /// no answer, because the only people in the record are the ones who happen
    /// to own a step.
    /// </summary>
    public IReadOnlyList<ExportPersonDto> People { get; init; } = [];

    public int PeopleCount => People.Count;
}

/// <summary>
/// One member of staff. No photo and no contact details: this record is handed
/// to a chatbot, and a face and an email address are not things it needs to
/// answer a question about who does what.
/// </summary>
public sealed record ExportPersonDto(
    int Id,
    string FullName,
    string? JobTitle,
    string Department,
    string StaffType);

/// <summary>A folder, and the ordered run of processes inside it.</summary>
public sealed record ExportFolderDto(
    int Id,
    string Name,
    IReadOnlyList<ExportProcessDto> Processes);

/// <summary>
/// One process. <c>Position</c> is where it sits in the folder's run - walking
/// off the end of one process's steps moves on to the next, and that order is
/// the thing a chatbot most needs to understand.
/// </summary>
public sealed record ExportProcessDto(
    int Id,
    int Position,
    string Name,
    string? Description,
    string? Department,
    string Status,
    IReadOnlyList<ExportStepDto> Steps);

public sealed record ExportStepDto(
    int Id,
    int Number,
    string Name,
    string? Description,
    string? Department,
    string Status,
    IReadOnlyList<string> ResponsiblePeople,
    IReadOnlyList<ExportScreenshotDto> Screenshots);

/// <summary>
/// An uploaded screenshot. <c>Url</c> is always present and always needs a
/// signed-in session to fetch. <c>DataUri</c> is only filled when the export was
/// asked for with image data, and is what makes the file self-contained.
/// </summary>
public sealed record ExportScreenshotDto(
    string FileName,
    string Url,
    long SizeBytes,
    DateTime UploadedAtUtc,
    string? DataUri);

public sealed record ExportDepartmentDto(int Id, string Name, string ColourHex, int PeopleCount);

public sealed record ExportStatusDto(int Id, string Name, string ColourHex);
