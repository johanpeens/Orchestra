using TeamPortal.Application.Abstractions;

namespace TeamPortal.Application.Common;

public static class AuthorisationGuard
{
    /// <summary>
    /// Null when the caller may go ahead, and a failed Result when they may not,
    /// so a service reads:
    /// <c>if (AuthorisationGuard.RequireEdit&lt;int&gt;(CurrentUser) is { } denied) return denied;</c>
    /// </summary>
    public static Result<T>? RequireEdit<T>(ICurrentUserService user) =>
        MayEdit(user) ? null : Denied<T>();

    /// <inheritdoc cref="RequireEdit{T}(ICurrentUserService)"/>
    public static Result? RequireEdit(ICurrentUserService user) =>
        MayEdit(user) ? null : Denied();

    /// <summary>Uploading a screenshot is open to Members as well as editors.</summary>
    public static Result<T>? RequireUpload<T>(ICurrentUserService user) =>
        MayUpload(user) ? null : Denied<T>();

    /// <inheritdoc cref="RequireUpload{T}(ICurrentUserService)"/>
    public static Result? RequireUpload(ICurrentUserService user) =>
        MayUpload(user) ? null : Denied();

    public static bool MayEdit(ICurrentUserService user)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Not signed in is not allowed. Anonymous writes would otherwise slip
        // through every check below, because a null user is in no role at all.
        return user.IsSignedIn &&
            (user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Manager));
    }

    public static bool MayUpload(ICurrentUserService user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return MayEdit(user) || user.IsInRole(AppRoles.Member);
    }

    /// <summary>
    /// One message for every refusal. It says what to do next rather than which
    /// role would have worked, which is not the caller's business.
    /// </summary>
    private const string Message =
        "You do not have permission to do that. Ask an administrator if you need it.";

    private static Result Denied() => Result.Invalid(ValidationError.FormLevel, Message);

    private static Result<T> Denied<T>() =>
        Result<T>.Invalid(new ValidationError(ValidationError.FormLevel, Message));
}
