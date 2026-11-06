using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;

namespace TeamPortal.Mock.Services;

public sealed partial class MockEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>Every message, oldest first.</summary>
    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    /// <summary>
    /// Set to make the next send fail, so the screens can be built against an
    /// SMTP server that is down. Consumed once, like MockOptions.FailNextWrite.
    /// </summary>
    public bool FailNextSend { get; set; }

    public Task<Result> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (FailNextSend)
        {
            FailNextSend = false;

            return Task.FromResult(Result.Invalid(
                ValidationError.FormLevel,
                "We could not send the email just now. Try again shortly, or contact an administrator."));
        }

        _sent.Enqueue(message);

        return Task.FromResult(Result.Success());
    }

    /// <summary>The most recent message sent to an address, or null.</summary>
    public EmailMessage? LastTo(string address) =>
        _sent.LastOrDefault(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Pulls a query parameter out of the link in a message - the token, usually.
    /// Reads the plain-text body, which is the one written as a bare URL.
    /// </summary>
    public static string? TokenFrom(EmailMessage message, string parameter = "token")
    {
        ArgumentNullException.ThrowIfNull(message);

        Match match = QueryValue(parameter).Match(message.TextBody);

        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    /// <summary>The absolute URL in a message, or null if it has none.</summary>
    public static string? LinkFrom(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Match match = AbsoluteUrl().Match(message.TextBody);

        return match.Success ? match.Value : null;
    }

    private static Regex QueryValue(string parameter) =>
        new($@"[?&]{Regex.Escape(parameter)}=([^\s&]+)", RegexOptions.None, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"https?://\S+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AbsoluteUrl();
}
