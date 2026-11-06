namespace TeamPortal.Infrastructure.Chat;

public enum ChatProvider
{
    /// <summary>
    /// The built-in retriever. No API key, no network call, and it cannot say
    /// anything that is not written up in the portal. The default, and the
    /// fallback whenever a configured provider has no key.
    /// </summary>
    Portal = 0,

    Gemini = 1,
    OpenAi = 2
}

public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    /// <summary>Portal, Gemini or OpenAi.</summary>
    public string Provider { get; set; } = nameof(ChatProvider.Portal);

    /// <summary>
    /// A key that applies whichever provider is selected. Usually left unset in
    /// favour of the per-provider blocks below, which let both credentials sit
    /// side by side so switching provider is one word.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Left blank, each provider uses its own current default - see
    /// <see cref="DefaultModelFor"/>. Set it to pin a version.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>Gemini's own key and model.</summary>
    public ProviderCredentials Gemini { get; set; } = new();

    /// <summary>OpenAI's own key and model.</summary>
    public ProviderCredentials OpenAi { get; set; } = new();

    /// <summary>
    /// The credentials for whichever provider is selected. The provider's own
    /// block wins; the top-level ApiKey/Model are the fallback, so a single-
    /// provider deployment need not nest anything.
    /// </summary>
    public ProviderCredentials Active => ResolvedProvider switch
    {
        ChatProvider.Gemini => Merge(Gemini),
        ChatProvider.OpenAi => Merge(OpenAi),
        _ => new ProviderCredentials { ApiKey = ApiKey, Model = Model }
    };

    private ProviderCredentials Merge(ProviderCredentials specific) => new()
    {
        ApiKey = Blank(specific.ApiKey) ? ApiKey : specific.ApiKey,
        Model = Blank(specific.Model) ? Model : specific.Model
    };

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Overrides the provider's base URL. For a proxy, a gateway, or an
    /// OpenAI-compatible endpoint that is not OpenAI.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// How much grounding JSON may be sent with a question. The portal export
    /// grows without limit as processes are written up, and an oversized request
    /// fails slowly and expensively. Past this the export is trimmed - see
    /// GroundedChatService.BuildGroundingAsync.
    /// </summary>
    public int MaxGroundingCharacters { get; set; } = 300_000;

    /// <summary>
    /// How many earlier turns to send back. Enough for "who is responsible for
    /// it?" to know what "it" was, without re-sending an hour of conversation.
    /// </summary>
    public int HistoryTurns { get; set; } = 8;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Lower is steadier. This is a question-answering job over a fixed
    /// document, not a writing one - invention is the failure mode.
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Whether to embed screenshots as base64 in the grounding. Off, and it
    /// should stay off unless the model is being asked about the images: it
    /// turns a readable payload into megabytes.
    /// </summary>
    public bool IncludeScreenshotData { get; set; }

    public ChatProvider ResolvedProvider =>
        Enum.TryParse(Provider, ignoreCase: true, out ChatProvider parsed)
            ? parsed
            : ChatProvider.Portal;

    /// <summary>
    /// Whether this configuration can actually reach a model. A provider named
    /// with no key is a misconfiguration, and the caller falls back to the
    /// built-in retriever rather than failing every question with a 401.
    /// </summary>
    public bool IsUsable =>
        ResolvedProvider != ChatProvider.Portal && !Blank(Active.ApiKey);

    public string DefaultModelFor(ChatProvider provider) =>
        !Blank(Active.Model)
            ? Active.Model!
            : provider switch
            {
                // gemini-2.0-flash was retired - the API answers a request for it
                // with a 404 telling you to move to this one.
                ChatProvider.Gemini => "gemini-3.6-flash",
                ChatProvider.OpenAi => "gpt-4o-mini",
                _ => string.Empty
            };
}

/// <summary>One provider's credentials, so several can sit side by side and
/// switching between them is a single word in configuration.</summary>
public sealed class ProviderCredentials
{
    public string? ApiKey { get; set; }

    public string? Model { get; set; }
}
