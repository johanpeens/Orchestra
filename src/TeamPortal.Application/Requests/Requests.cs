using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;

namespace TeamPortal.Application.Requests;

// --- Organisation ------------------------------------------------------------

/// <summary>Edit Department modal - screen 07.</summary>
public sealed record CreateDepartmentRequest(
    string Name,
    string ColourHex);

public sealed record UpdateDepartmentRequest(
    int Id,
    string Name,
    string ColourHex);

/// <summary>Add Employee modal - screen 06.</summary>
public sealed record CreateEmployeeRequest(
    string FullName,
    int DepartmentId,
    string? JobTitle = null,
    string? Email = null,
    StaffType StaffType = StaffType.Employee,
    FileUpload? Photo = null);

public sealed record UpdateEmployeeRequest(
    int Id,
    string FullName,
    int DepartmentId,
    string? JobTitle = null,
    string? Email = null,
    StaffType StaffType = StaffType.Employee,
    bool IsActive = true,
    FileUpload? Photo = null);

// --- Processes ---------------------------------------------------------------

public sealed record CreateProcessFolderRequest(string Name);

/// <summary>Add Process modal - screen 03.</summary>
public sealed record CreateProcessRequest(
    string Name,
    int ProcessFolderId,
    string? Description = null,
    int? DepartmentId = null);

public sealed record UpdateProcessRequest(
    int Id,
    string Name,
    string? Description = null,
    int? DepartmentId = null,
    int? StepStatusId = null);

/// <summary>
/// Add Step modal - screen 04. <paramref name="OwnerIds"/> is the chip list behind
/// "Add Team Member"; duplicates are rejected rather than silently collapsed, so the
/// UI never quietly loses a selection the user thinks they made.
/// </summary>
public sealed record CreateStepRequest(
    int ProcessId,
    string Name,
    string? Description = null,
    int? DepartmentId = null,
    IReadOnlyList<int>? OwnerIds = null,
    FileUpload? Screenshot = null)
{
    public IReadOnlyList<int> Owners => OwnerIds ?? [];
}

public sealed record UpdateStepRequest(
    int Id,
    string Name,
    string? Description = null,
    int? DepartmentId = null,
    int? StepStatusId = null,
    IReadOnlyList<int>? OwnerIds = null,

    /// <summary>
    /// A new screenshot, replacing whatever is on the step. Null leaves the
    /// existing one alone - it is not "remove the screenshot".
    ///
    /// This was missing, and the dialog quietly dropped every upload made while
    /// EDITING a step. Only brand-new steps could carry one.
    /// </summary>
    FileUpload? Screenshot = null)
{
    public IReadOnlyList<int> Owners => OwnerIds ?? [];
}

/// <summary>Drag-to-reorder on the flow strip.</summary>
public sealed record MoveStepRequest(int StepId, int NewStepNumber);

// --- Support -----------------------------------------------------------------

/// <summary>The login form - screen 01.</summary>
public sealed record SignInRequest(
    string Email,
    string Password,
    bool RememberMe = false);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword);

/// <summary>The registration form. A self-registered account starts read-only.</summary>
public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword);

/// <summary>What arrives back on the link in the confirmation email.</summary>
public sealed record ConfirmEmailRequest(string Email, string Token);
