using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;

namespace TeamPortal.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 465 is implicit TLS, 587 is STARTTLS, 25 is usually a local relay with no
    /// encryption at all. <see cref="Security"/> is worked out from this unless
    /// it is set explicitly.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>Blank for a relay that authenticates by IP rather than by account.</summary>
    public string? User { get; set; }

    /// <summary>
    /// Never in appsettings.json. User secrets in DEV, the pipeline's secret
    /// store above it - see Frontend/BuildPlan.md.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>The address messages come from. Usually the same as User.</summary>
    public string From { get; set; } = string.Empty;

    public string FromName { get; set; } = "Orchestra";

    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Overrides the port-based guess. Leave unset unless a server does something
    /// unusual - a value here that disagrees with the port is how you end up
    /// debugging a handshake at four in the afternoon.
    /// </summary>
    public SecureSocketOptions? Security { get; set; }

    /// <summary>
    /// True when there is enough here to try at all. A half-configured relay
    /// should say so rather than swallow every confirmation email.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);

    /// <summary>
    /// The handshake this port implies. 465 must be SslOnConnect: the server
    /// starts TLS immediately and never sends a plaintext greeting, so a client
    /// waiting to negotiate STARTTLS just hangs until it times out.
    /// </summary>
    public SecureSocketOptions ResolveSecurity() =>
        Security ?? Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto
        };
}

internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _logger = logger;
    }

    public async Task<Result> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.IsConfigured)
        {
            // Not an exception: the caller decides what an unsendable email
            // means, and for registration it means "tell them to contact an
            // admin" rather than "lose the account you just created".
            _logger.LogError(
                "No SMTP host or from-address is configured, so '{Subject}' was not sent.",
                message.Subject);

            return Failed();
        }

        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(_options.FromName, _options.From));
        mail.To.Add(MailboxAddress.Parse(message.To));
        mail.Subject = message.Subject;

        // Both parts. A message with no text alternative is scored as spam by
        // most filters, and some corporate clients strip HTML outright.
        mail.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = (int)TimeSpan.FromSeconds(_options.TimeoutSeconds).TotalMilliseconds
        };

        try
        {
            await client.ConnectAsync(
                _options.Host, _options.Port, _options.ResolveSecurity(), ct);

            if (!string.IsNullOrWhiteSpace(_options.User))
            {
                // Empty rather than null: a relay that wants a username but no
                // password is unusual but legal, and MailKit will not take null.
                await client.AuthenticateAsync(
                    _options.User, _options.Password ?? string.Empty, ct);
            }

            await client.SendAsync(mail, ct);
            await client.DisconnectAsync(quit: true, ct);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Sent '{Subject}' to {Recipient}.", message.Subject, message.To);
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is SmtpCommandException
                                      or SmtpProtocolException
                                      or AuthenticationException
                                      or SslHandshakeException
                                      or IOException
                                      or SocketException)
        {
            // The recipient must never see why. "Mailbox unavailable" tells an
            // attacker which addresses exist, which is exactly what the
            // deliberately vague registration and forgot-password responses are
            // there to prevent. The detail goes to the log instead.
            _logger.LogError(
                ex,
                "SMTP refused '{Subject}' for {Recipient} via {Host}:{Port} ({Security}).",
                message.Subject, message.To, _options.Host, _options.Port,
                _options.ResolveSecurity());

            return Failed();
        }
    }

    private static Result Failed() =>
        Result.Invalid(
            ValidationError.FormLevel,
            "We could not send the email just now. Try again shortly, or contact an administrator.");
}
