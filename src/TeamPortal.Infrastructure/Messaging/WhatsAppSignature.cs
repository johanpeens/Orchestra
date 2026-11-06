using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TeamPortal.Infrastructure.Messaging;

/// <summary>
/// Proves an incoming webhook actually came from Twilio.
///
/// This is not optional. The endpoint has to be anonymous - Twilio has no
/// cookie - so without a signature check the URL is a public box that answers
/// questions about the business to anybody who finds it.
/// </summary>
public static class WhatsAppSignature
{
    /// <summary>
    /// Twilio's scheme: take the full URL it called, append every POST field as
    /// key then value with nothing between, sorted by key, and HMAC-SHA1 the
    /// result with the auth token.
    /// </summary>
    /// <param name="url">
    /// The URL TWILIO used, which is not always the one this app sees. Behind a
    /// tunnel or a proxy the scheme and host differ, and the signature is over
    /// Twilio's version - hence WhatsApp:PublicWebhookUrl.
    /// </param>
    [SuppressMessage(
        "Security",
        "CA5350:Do not use weak cryptographic algorithms",
        Justification =
            "HMAC-SHA1 is not a choice here - it is Twilio's signature scheme, and " +
            "verifying their signature means computing the same thing they did. " +
            "Using anything stronger would simply never match. The weakness is " +
            "also not load-bearing: this verifies a short-lived webhook against a " +
            "shared secret, it does not protect data at rest.")]
    public static bool IsFromTwilio(
        string url,
        IEnumerable<KeyValuePair<string, string>> form,
        string? signatureHeader,
        string authToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(authToken))
        {
            return false;
        }

        StringBuilder payload = new(url);

        foreach (KeyValuePair<string, string> field in
            form.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            payload.Append(field.Key).Append(field.Value);
        }

        using HMACSHA1 hmac = new(Encoding.UTF8.GetBytes(authToken));

        string expected = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));

        // Fixed-time. A plain string compare leaks how much of the signature was
        // right, one character at a time.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signatureHeader));
    }

    /// <summary>
    /// Splits an answer into pieces Twilio will accept, breaking on line ends
    /// and then on spaces so a step list does not get cut mid-word.
    /// </summary>
    public static IReadOnlyList<string> Chunk(string text, int limit)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length <= limit)
        {
            return [text];
        }

        List<string> parts = [];
        string rest = text;

        while (rest.Length > limit)
        {
            int cut = rest.LastIndexOf('\n', limit - 1);

            if (cut <= 0)
            {
                cut = rest.LastIndexOf(' ', limit - 1);
            }

            if (cut <= 0)
            {
                cut = limit;
            }

            parts.Add(rest[..cut].TrimEnd());
            rest = rest[cut..].TrimStart();
        }

        if (rest.Length > 0)
        {
            parts.Add(rest);
        }

        // Numbered, because WhatsApp shows them as separate bubbles and a step
        // list arriving in three pieces is otherwise just confusing.
        return parts.Count == 1
            ? parts
            : [.. parts.Select((p, i) =>
                $"({(i + 1).ToString(CultureInfo.InvariantCulture)}/{parts.Count}) {p}")];
    }
}
