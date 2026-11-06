namespace TeamPortal.Application.Enums;

/// <summary>
/// Stored as int. Never renumber an existing value - only append new ones.
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

/// <summary>Which rows the admin grid is showing.</summary>
public enum ApprovalFilter
{
    All = 0,

    /// <summary>Confirmed their email and waiting on a decision.</summary>
    AwaitingDecision = 1,

    Approved = 2,

    Rejected = 3
}
