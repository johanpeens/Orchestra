using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TeamPortal.Infrastructure.Messaging;

/// <summary>
/// Sends a WhatsApp message. One method, because that is the whole of what a
/// channel has to do - which is also what makes swapping Twilio for Meta's
/// Cloud API later a new class and a line of registration rather than a rework.
/// </summary>
public interface IWhatsAppSender
{
    Task SendAsync(string toNumber, string body, CancellationToken ct = default);
}

internal sealed class TwilioWhatsAppSender : IWhatsAppSender
{
    private const string ApiRoot = "https://api.twilio.com/2010-04-01";

    private readonly HttpClient _http;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<TwilioWhatsAppSender> _log;

    public TwilioWhatsAppSender(
        HttpClient http, WhatsAppOptions options, ILogger<TwilioWhatsAppSender> log)
    {
        _http = http;
        _options = options;
        _log = log;
    }

    public async Task SendAsync(string toNumber, string body, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        // Split before sending. Twilio rejects an over-long body outright, and
        // an answer listing a whole roster goes past the limit easily.
        foreach (string part in
            WhatsAppSignature.Chunk(body, _options.MaxMessageCharacters))
        {
            await SendOneAsync(toNumber, part, ct).ConfigureAwait(false);
        }
    }

    private async Task SendOneAsync(string toNumber, string body, CancellationToken ct)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, $"{ApiRoot}/Accounts/{_options.AccountSid}/Messages.json");

        // Basic auth, Twilio's scheme: the account SID as the user and the auth
        // token as the password.
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.AccountSid}:{_options.AuthToken}")));

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = _options.FromNumber ?? string.Empty,
            ["To"] = toNumber,
            ["Body"] = body
        });

        using HttpResponseMessage response =
            await _http.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // The status only. A failed Twilio response echoes the message body
            // back, and that body is an answer about the business.
            _log.LogWarning(
                "Twilio refused a WhatsApp message ({Status} {Reason}).",
                (int)response.StatusCode,
                response.ReasonPhrase);
        }
    }
}
