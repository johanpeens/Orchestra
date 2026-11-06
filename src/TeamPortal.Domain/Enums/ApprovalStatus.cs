namespace TeamPortal.Domain.Enums;

/// <summary>
/// Stored as int. The explicit numbers matter - never renumber an existing
/// value, only append new ones.
///
/// Confirming an email proves somebody can read that mailbox. It does not prove
/// they should be in this portal, which is what approval is for. The two gates
/// are separate on purpose: one is automatic, the other is a person deciding.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>Registered and waiting. Cannot sign in.</summary>
    Pending = 1,

    /// <summary>An administrator let them in.</summary>
    Approved = 2,

    /// <summary>An administrator turned them down. Cannot sign in.</summary>
    Rejected = 3
}
