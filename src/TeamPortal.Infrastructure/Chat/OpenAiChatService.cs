using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Services;

namespace TeamPortal.Infrastructure.Chat;

internal sealed class OpenAiChatService : GroundedChatService
{
    private const string DefaultEndpoint = "https://api.openai.com/v1";

    private readonly HttpClient _http;

    public OpenAiChatService(
        HttpClient http,
        IPortalExportService export,
        [FromKeyedServices(ChatRegistration.FallbackKey)] IChatService fallback,
        ChatOptions options,
        ILogger<OpenAiChatService> log)
        : base(export, fallback, options, log)
        => _http = http;

    protected override async Task<string> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<ChatMessageDto> history,
        string question,
        CancellationToken ct)
    {
        string root = (Options.Endpoint ?? DefaultEndpoint).TrimEnd('/');

        using HttpRequestMessage request = new(HttpMethod.Post, $"{root}/chat/completions");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", Options.Active.ApiKey);

        request.Content = JsonContent.Create(new OpenAiRequest(
            Options.DefaultModelFor(ChatProvider.OpenAi),
            [
                new OpenAiMessage("system", systemPrompt),
                .. history.Select(m => new OpenAiMessage(
                    m.Role == ChatRole.Person ? "user" : "assistant", m.Text)),
                new OpenAiMessage("user", question)
            ],
            Options.Temperature));

        using HttpResponseMessage response =
            await _http.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ChatProviderException(
                $"The model refused the request ({(int)response.StatusCode} {response.ReasonPhrase}).");
        }

        OpenAiResponse? body;

        try
        {
            body = await response.Content
                .ReadFromJsonAsync<OpenAiResponse>(ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new ChatProviderException("The model's response could not be read.", ex);
        }

        OpenAiChoice? choice =
            body?.Choices is { Count: > 0 } choices ? choices[0] : null;

        return TextOrThrow(choice?.Message?.Content, "The model");
    }

    // --- Wire format ----------------------------------------------------------

    private sealed record OpenAiRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record OpenAiMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAiResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChoice>? Choices);

    private sealed record OpenAiChoice(
        [property: JsonPropertyName("message")] OpenAiMessage? Message);
}
