namespace TeamPortal.Domain.Enums;

/// <summary>
/// Stored as int. The explicit numbers matter - never renumber an existing
/// value, only append new ones. Mirrors TeamPortal.Application.Enums.StaffType;
/// the mapping between the two lives in the DTO projections in Application.
/// </summary>
public enum StaffType
{
    Employee = 1,
    Management = 2
}
