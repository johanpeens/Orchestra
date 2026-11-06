using TeamPortal.Application.Common;

namespace TeamPortal.Application.Abstractions;

/// <summary>One outgoing message. Both bodies are supplied - see EmailBodies.</summary>
/// <param name="To">A single recipient. The app never sends to more than one.</param>
/// <param name="Subject">Plain text, one line.</param>
/// <param name="HtmlBody">What most clients will show.</param>
/// <param name="TextBody">
/// The fallback. Not optional: a message with no text part is scored as spam by
/// most filters, and some corporate clients strip HTML outright.
/// </param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);

public interface IEmailSender
{
    /// <summary>
    /// Sends one message. Returns a failed <see cref="Result"/> rather than
    /// throwing, because the caller usually has to carry on regardless - a
    /// registration that succeeded but could not send its confirmation is still a
    /// registration, and the user needs telling either way.
    /// </summary>
    Task<Result> SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// Where the app lives, so a background service can build a link back to it.
/// A request cannot be relied on for this: the confirmation email is composed
/// server-side, and a Host header is attacker-controlled.
/// </summary>
public interface IAppUrlProvider
{
    /// <summary>Absolute, no trailing slash, e.g. <c>https://portal.company.com</c>.</summary>
    string BaseUrl { get; }

    /// <summary>Absolute URL for a path like <c>/confirm-email</c>.</summary>
    string Absolute(string relativePath, params (string Key, string Value)[] query);
}
