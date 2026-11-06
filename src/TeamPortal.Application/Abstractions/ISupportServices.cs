using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;

namespace TeamPortal.Application.Abstractions;

/// <summary>
/// Sign-in. Screen 01 talks to this and never to SignInManager, which is what lets
/// phase B3 swap Identity in underneath without touching the login page.
/// </summary>
public interface IAuthService
{
    Task<SignInResultDto> SignInAsync(SignInRequest request, CancellationToken ct = default);

    Task SignOutAsync(CancellationToken ct = default);

    /// <summary>
    /// Always succeeds from the caller's point of view, whether or not the address is
    /// known. Reporting "no such account" would let an attacker enumerate users.
    /// </summary>
    Task<Result> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates an unconfirmed, read-only account and emails a confirmation link.
    ///
    /// Like <see cref="RequestPasswordResetAsync"/>, this reports success even
    /// when the address is already registered - the difference goes in the email,
    /// not in the response, so the form cannot be used to test which addresses
    /// have accounts.
    /// </summary>
    Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>
    /// Completes registration from the link in the email. Safe to call twice: an
    /// already-confirmed account succeeds rather than erroring, because people
    /// click links twice and a second click is not a failure.
    /// </summary>
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);
}

/// <summary>Who is signed in right now. Backs the app bar and the audit columns.</summary>
public interface ICurrentUserService
{
    CurrentUserDto? User { get; }

    int? UserId { get; }

    bool IsSignedIn { get; }

    bool IsInRole(string role);
}

/// <summary>
/// Where uploaded screenshots live. MockFileStore in Part A, LocalDiskFileStore and
/// AzureBlobFileStore in Part B. Validation rules live in the Application layer and
/// are applied by every implementation.
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Validates and stores the file, returning the path or blob key to persist in
    /// <c>StepImage.StoredPath</c>. The stored name is generated - the caller's
    /// filename is never used on disk.
    /// </summary>
    Task<Result<string>> SaveAsync(FileUpload file, CancellationToken ct = default);

    /// <summary>The URL a browser can fetch the stored file from.</summary>
    string GetUrl(string storedPath);

    Task<Result> DeleteAsync(string storedPath, CancellationToken ct = default);
}

/// <summary>
/// Reads a stored file back. Separate from IFileStore because only the endpoint
/// that serves images needs it, and it must not be reachable from a component -
/// the authorisation check lives at the endpoint, not here.
/// </summary>
public interface IFileReader
{
    /// <summary>The file's bytes, or null if it is not there or not ours.</summary>
    Stream? OpenRead(string storedPath);
}

public interface IActivityLogService
{
    /// <summary>"View Process History" - the log for one record, newest first.</summary>
    Task<PagedResult<ActivityLogEntryDto>> GetForEntityAsync(
        string entityName, int entityId, int page = 1, int pageSize = 25, CancellationToken ct = default);

    /// <summary>"View Activity Log" - everything, newest first.</summary>
    Task<PagedResult<ActivityLogEntryDto>> GetRecentAsync(
        int page = 1, int pageSize = 25, CancellationToken ct = default);

    /// <summary>Append-only. Every write path in the app calls this.</summary>
    Task WriteAsync(
        ActivityAction action, string entityName, int? entityId, string summary, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetForCurrentUserAsync(
        bool unreadOnly = false, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(CancellationToken ct = default);

    /// <summary>Only ever affects the signed-in user's own notifications.</summary>
    Task<Result> MarkAsReadAsync(int notificationId, CancellationToken ct = default);

    Task<Result> MarkAllAsReadAsync(CancellationToken ct = default);
}
