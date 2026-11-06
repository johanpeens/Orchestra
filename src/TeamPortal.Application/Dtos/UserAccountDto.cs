using TeamPortal.Application.Enums;

namespace TeamPortal.Application.Dtos;

public sealed record UserAccountDto(
    int UserId,
    string FullName,
    string Email,
    bool EmailConfirmed,
    ApprovalStatus ApprovalStatus,
    IReadOnlyList<string> Roles,
    DateTime? RegisteredAt,
    DateTime? ApprovalDecidedAt,
    string? ApprovalDecidedBy,
    DateTime? LastLoginAt)
{
    /// <summary>
    /// Waiting on an administrator. Confirmed their address, nobody has decided
    /// yet - the only state the queue is actually about.
    /// </summary>
    public bool IsAwaitingDecision =>
        EmailConfirmed && ApprovalStatus == ApprovalStatus.Pending;

    /// <summary>
    /// Registered but never clicked the link. Shown in the grid greyed out, so an
    /// admin can see the difference between "waiting on me" and "waiting on them".
    /// </summary>
    public bool IsAwaitingConfirmation =>
        !EmailConfirmed && ApprovalStatus == ApprovalStatus.Pending;

    public string PrimaryRole => Roles.Count > 0 ? Roles[0] : "None";

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
