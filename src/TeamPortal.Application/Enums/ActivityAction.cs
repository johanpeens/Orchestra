namespace TeamPortal.Application.Enums;

/// <summary>
/// Stored as int. Never renumber an existing value - only append new ones.
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
