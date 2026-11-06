using TeamPortal.Application.Enums;

namespace TeamPortal.Application.Dtos;

/// <summary>
/// A row in the department filter column on screen 05, e.g. "Business Enablement (7)".
/// <paramref name="EmployeeCount"/> is calculated, never stored.
/// </summary>
public sealed record DepartmentListItemDto(
    int Id,
    string Name,
    string ColourHex,
    int EmployeeCount);

/// <summary>Everything the Edit Department modal (screen 07) needs to open pre-filled.</summary>
public sealed record DepartmentDetailDto(
    int Id,
    string Name,
    string ColourHex);

/// <summary>One employee card on screen 05.</summary>
public sealed record EmployeeCardDto(
    int Id,
    string FullName,
    string? JobTitle,
    string DepartmentName,
    string DepartmentColour,
    string? PhotoUrl,
    StaffType StaffType)
{
    /// <summary>Fallback for the avatar when there is no photo, e.g. "DC" for David Chen.</summary>
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

    public bool IsManagement => StaffType == StaffType.Management;
}

/// <summary>Everything the Add/Edit Employee modal (screen 06) needs to open pre-filled.</summary>
public sealed record EmployeeDetailDto(
    int Id,
    string FullName,
    string? JobTitle,
    string? Email,
    string? PhotoUrl,
    StaffType StaffType,
    int DepartmentId,
    bool IsActive);

/// <summary>An employee reduced to what an owner-picker needs.</summary>
public sealed record EmployeeOptionDto(
    int Id,
    string FullName,
    string? JobTitle,
    string? PhotoUrl)
{
    /// <summary>What MudAutocomplete shows in the dropdown.</summary>
    public string Display => JobTitle is null ? FullName : $"{FullName} - {JobTitle}";
}
