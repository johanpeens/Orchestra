using TeamPortal.Application.Enums;

namespace TeamPortal.Application.Dtos;

/// <summary>The signed-in user, as the app bar and authorisation see them.</summary>
public sealed record CurrentUserDto(
    int UserId,
    string FullName,
    string Email,
    string? PhotoUrl,
    IReadOnlyList<string> Roles)
{
    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Why a sign-in attempt failed. The UI shows the same message for
/// <see cref="InvalidCredentials"/> whether the email or the password was wrong -
/// telling them apart would let an attacker discover which accounts exist.
/// </summary>
public enum SignInFailureReason
{
    None = 0,
    InvalidCredentials = 1,
    LockedOut = 2,
    Inactive = 3,

    /// <summary>Registered, but the confirmation link has not been clicked.</summary>
    EmailNotConfirmed = 4,

    /// <summary>Confirmed, but no administrator has approved the account yet.</summary>
    AwaitingApproval = 5,

    /// <summary>An administrator turned the request down.</summary>
    ApprovalRejected = 6
}

/// <summary>The outcome of a sign-in attempt.</summary>
public sealed record SignInResultDto(
    bool Succeeded,
    SignInFailureReason Reason,
    CurrentUserDto? User,
    DateTimeOffset? LockoutEnd)
{
    /// <summary>
    /// A single-use handoff for the endpoint that writes the authentication
    /// cookie. Null on failure, and null for any implementation that has no
    /// cookie to write.
    ///
    /// It exists because an interactive Blazor Server circuit CANNOT set a
    /// cookie: the HTTP response finished when the page was first served, and
    /// the circuit is a WebSocket after that. Signing in from a component throws
    /// "Headers are read-only, response has already started". The credentials
    /// are checked here, in the circuit, and only an opaque ticket travels back
    /// through the browser to a real request that can write the header.
    /// </summary>
    public string? Ticket { get; init; }

    public static SignInResultDto Success(CurrentUserDto user, string? ticket = null) =>
        new(true, SignInFailureReason.None, user, null) { Ticket = ticket };

    public static SignInResultDto Failed(SignInFailureReason reason, DateTimeOffset? lockoutEnd = null) =>
        new(false, reason, null, lockoutEnd);

    /// <summary>The message shown on the login screen. Deliberately vague on credentials.</summary>
    public string Message => Reason switch
    {
        SignInFailureReason.None => string.Empty,
        SignInFailureReason.InvalidCredentials => "That email address and password do not match.",
        SignInFailureReason.LockedOut => "Too many failed attempts. Try again in 15 minutes, or contact an administrator.",
        SignInFailureReason.Inactive => "That email address and password do not match.",
        SignInFailureReason.EmailNotConfirmed =>
            "Confirm your email address before signing in. Check your inbox for the link we sent.",
        SignInFailureReason.AwaitingApproval =>
            "Your account is waiting for an administrator to approve it. We will email you when it is ready.",
        // Deliberately the same wording as a wrong password. Someone who was
        // turned down does not need to be told so every time they try.
        SignInFailureReason.ApprovalRejected =>
            "That email address and password do not match.",
        _ => "Something went wrong. Please try again."
    };
}

/// <summary>One row in "View Activity Log" or "View Process History".</summary>
public sealed record ActivityLogEntryDto(
    long Id,
    string? UserFullName,
    ActivityAction Action,
    string EntityName,
    int? EntityId,
    string Summary,
    DateTime HappenedAt);

/// <summary>One item behind the notification bell.</summary>
public sealed record NotificationDto(
    int Id,
    string Title,
    string? Message,
    string? LinkUrl,
    bool IsRead,
    DateTime CreatedAt);

/// <summary>A slice of a longer list. Every list that can grow is paged.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int pageSize) => new([], 0, 1, pageSize);
}

/// <summary>
/// A file handed to <see cref="Abstractions.IFileStore"/>. Wraps the stream so the
/// Application layer never has to know about <c>IBrowserFile</c> or ASP.NET types.
/// </summary>
public sealed record FileUpload(
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);
