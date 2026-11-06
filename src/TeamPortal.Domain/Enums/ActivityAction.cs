namespace TeamPortal.Domain.Enums;

/// <summary>
/// Stored as int. The explicit numbers matter - never renumber an existing
/// value, only append new ones. Mirrors TeamPortal.Application.Enums.ActivityAction.
/// </summary>
public enum ActivityAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    Viewed = 4,
    LoggedIn = 5,
    LoggedOut = 6,
    Uploaded = 7
}
