namespace TeamPortal.Application.Common;

public static class AppRoles
{
    /// <summary>Everything, including users and business units.</summary>
    public const string Admin = "Admin";

    /// <summary>Create and edit processes, steps, employees and departments.</summary>
    public const string Manager = "Manager";

    /// <summary>View everything, upload step screenshots.</summary>
    public const string Member = "Member";

    /// <summary>Read-only.</summary>
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, Member, Viewer];

    /// <summary>Roles allowed to create and edit business data.</summary>
    public const string CanEdit = $"{Admin},{Manager}";

    /// <summary>Roles allowed to upload a step screenshot.</summary>
    public const string CanUpload = $"{Admin},{Manager},{Member}";
}
