namespace TeamPortal.Infrastructure.Messaging;

/// <summary>
/// Twilio's WhatsApp API. Twilio rather than Meta's Cloud API on purpose: the
/// sandbox is reachable in minutes with no business verification, which is what
/// a proof of concept needs. Meta is the cheaper endpoint at volume and the
/// swap is a different base URL and auth header - see IWhatsAppSender.
/// </summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>Off unless switched on. An unconfigured webhook answers nobody.</summary>
    public bool Enabled { get; set; }

    public string? AccountSid { get; set; }

    /// <summary>
    /// Never in appsettings.json - user secrets, like every other credential
    /// here. It also signs incoming webhooks, so leaking it lets anyone forge a
    /// message that looks like it came from Twilio.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// The sending number, Twilio's format: "whatsapp:+14155238886" is the
    /// shared sandbox number.
    /// </summary>
    public string? FromNumber { get; set; }

    /// <summary>
    /// The public HTTPS URL Twilio posts to. Needed because Twilio signs the
    /// URL it called, and behind a proxy or a tunnel the URL this app sees is
    /// not the one Twilio used - see WhatsAppSignature.
    /// </summary>
    public string? PublicWebhookUrl { get; set; }

    /// <summary>
    /// How long a conversation stays warm. Long enough for "who is responsible
    /// for it?" to know what "it" was; short enough that a phone left on a desk
    /// does not carry a thread into tomorrow.
    /// </summary>
    public int ConversationMinutes { get; set; } = 60;

    /// <summary>
    /// Twilio rejects a message body over 1600 characters. Answers about a
    /// whole roster run longer than that, so they are split rather than cut.
    /// </summary>
    public int MaxMessageCharacters { get; set; } = 1500;

    /// <summary>
    /// Twilio abandons a webhook that takes too long. The answer is generated
    /// inside the request, so this caps it below their limit and sends an
    /// apology rather than nothing.
    /// </summary>
    public int AnswerTimeoutSeconds { get; set; } = 12;

    public bool IsUsable =>
        Enabled
        && !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && !string.IsNullOrWhiteSpace(FromNumber);
}
