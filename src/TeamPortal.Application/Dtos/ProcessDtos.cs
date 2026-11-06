namespace TeamPortal.Application.Dtos;

/// <summary>A folder row in the left sidebar of screen 02, e.g. "The Business (9)".</summary>
public sealed record ProcessFolderDto(
    int Id,
    string Name,
    int ProcessCount);

/// <summary>A process in the sidebar list, before it is opened.</summary>
/// <summary>
/// One card in the process flow strip on screen 02. A folder holds a sequence of
/// processes; each process holds a sequence of steps. Walking off the end of one
/// process's steps moves on to the next process.
/// </summary>
public sealed record ProcessListItemDto(
    int Id,
    string Name,
    string? Description,
    int SortOrder,
    int? DepartmentId,
    string? DepartmentName,
    string? DepartmentColour,
    int StepStatusId,
    string StatusName,
    string StatusColour,
    int StepCount);

/// <summary>
/// One process with its full step list - everything the detail panel on screen 02
/// needs in a single call. Steps arrive in <c>StepNumber</c> order.
/// </summary>
public sealed record ProcessDetailDto(
    int Id,
    string Name,
    string? Description,
    int ProcessFolderId,
    int? DepartmentId,
    string? DepartmentName,
    int StepStatusId,
    IReadOnlyList<ProcessStepDto> Steps)
{
    public bool HasSteps => Steps.Count > 0;
}

/// <summary>One numbered step inside a process.</summary>
public sealed record ProcessStepDto(
    int Id,
    int StepNumber,
    string Name,
    string? Description,
    int? DepartmentId,
    string? DepartmentName,
    string? DepartmentColour,
    int StepStatusId,
    string StatusName,
    string StatusColour,
    IReadOnlyList<StepOwnerDto> Owners,
    IReadOnlyList<StepImageDto> Images)
{
    public bool HasOwners => Owners.Count > 0;

    public bool HasImages => Images.Count > 0;
}

/// <summary>A person in the "Responsible Persons" list on a step.</summary>
public sealed record StepOwnerDto(
    int EmployeeId,
    string FullName,
    string? JobTitle,
    string? PhotoUrl,
    bool IsPrimary)
{
    public string Initials
    {
        get
        {
            string[] parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            };
        }
    }
}

/// <summary>An uploaded reference screenshot attached to a step.</summary>
public sealed record StepImageDto(
    int Id,
    string FileName,
    string Url,
    long SizeBytes,
    DateTime UploadedAt);

/// <summary>A selectable status - the coloured dot on a step card.</summary>
public sealed record StepStatusDto(
    int Id,
    string Name,
    string ColourHex,
    bool IsDefault);
