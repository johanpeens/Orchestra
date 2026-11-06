using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Services;

namespace TeamPortal.Infrastructure.Chat;

internal sealed class GeminiChatService : GroundedChatService
{
    private const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta";

    private readonly HttpClient _http;

    public GeminiChatService(
        HttpClient http,
        IPortalExportService export,
        [FromKeyedServices(ChatRegistration.FallbackKey)] IChatService fallback,
        ChatOptions options,
        ILogger<GeminiChatService> log)
        : base(export, fallback, options, log)
        => _http = http;

    protected override async Task<string> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<ChatMessageDto> history,
        string question,
        CancellationToken ct)
    {
        string model = Options.DefaultModelFor(ChatProvider.Gemini);
        string root = (Options.Endpoint ?? DefaultEndpoint).TrimEnd('/');

        // The key goes in a header, never in the query string: a URL ends up in
        // access logs, in proxies and in exception messages.
        using HttpRequestMessage request =
            new(HttpMethod.Post, $"{root}/models/{model}:generateContent");

        request.Headers.Add("x-goog-api-key", Options.Active.ApiKey);

        request.Content = JsonContent.Create(new GeminiRequest(
            new GeminiContent("user", [new GeminiPart(systemPrompt)]),
            [
                .. history.Select(m => new GeminiContent(
                    m.Role == ChatRole.Person ? "user" : "model",
                    [new GeminiPart(m.Text)])),
                new GeminiContent("user", [new GeminiPart(question)])
            ],
            new GeminiConfig(Options.Temperature)));

        using HttpResponseMessage response =
            await _http.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // The status and Google's own reason, not the body wholesale - a
            // failed request can echo parts of the prompt back.
            throw new ChatProviderException(
                $"Gemini refused the request ({(int)response.StatusCode} {response.ReasonPhrase}).");
        }

        GeminiResponse? body;

        try
        {
            body = await response.Content
                .ReadFromJsonAsync<GeminiResponse>(ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new ChatProviderException("Gemini's response could not be read.", ex);
        }

        // The first part WITH TEXT, not simply the first part. A thinking model
        // can put a thought part in front of the answer, and that one carries a
        // thoughtSignature and no text at all.
        string? text = body?.Candidates is { Count: > 0 } candidates
            ? candidates[0].Content?.Parts?
                .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part.Text))?.Text
            : null;

        return TextOrThrow(text, "Gemini");
    }

    // --- Wire format ----------------------------------------------------------

    private sealed record GeminiRequest(
        [property: JsonPropertyName("system_instruction")] GeminiContent SystemInstruction,
        [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents,
        [property: JsonPropertyName("generationConfig")] GeminiConfig GenerationConfig);

    private sealed record GeminiContent(
        [property: JsonPropertyName("role")] string? Role,
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

    /// <summary>
    /// Text is nullable because a thinking model returns parts that carry only a
    /// thoughtSignature. Those are skipped rather than deserialised into an
    /// empty answer.
    /// </summary>
    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string? Text);

    private sealed record GeminiConfig(
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record GeminiResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates);

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content);
}
