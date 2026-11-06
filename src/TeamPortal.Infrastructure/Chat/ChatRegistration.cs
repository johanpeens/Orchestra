using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Services;

namespace TeamPortal.Infrastructure.Chat;

public static class ChatRegistration
{
    /// <summary>
    /// Service key for the built-in retriever. Keyed so a provider can depend on
    /// it without depending on itself - both are IChatService.
    /// </summary>
    public const string FallbackKey = "portal-retriever";

    /// <summary>
    /// Registers the chat. With no provider configured, or a provider named with
    /// no API key, the built-in retriever answers on its own and nothing calls
    /// out to anything.
    /// </summary>
    public static IServiceCollection AddChat(
        this IServiceCollection services, ChatOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);

        // AddApplicationValidators registers the retriever as a default. This
        // call is the decision, so it takes the registration over outright
        // rather than relying on which of the two ran last.
        services.RemoveAll<IChatService>();

        // Always present, under the key, as the thing a provider falls back to.
        services.AddKeyedScoped<IChatService, PortalChatService>(FallbackKey);

        if (!options.IsUsable)
        {
            // Named a provider but gave it no key? That is a misconfiguration,
            // and answering from the portal is a better response to it than
            // returning 401 to every question.
            services.AddScoped<IChatService>(
                sp => sp.GetRequiredKeyedService<IChatService>(FallbackKey));

            return services;
        }

        TimeSpan timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 300));

        switch (options.ResolvedProvider)
        {
            case ChatProvider.Gemini:
                services.AddHttpClient<GeminiChatService>(c => c.Timeout = timeout);
                services.AddScoped<IChatService>(sp => sp.GetRequiredService<GeminiChatService>());
                break;

            case ChatProvider.OpenAi:
                services.AddHttpClient<OpenAiChatService>(c => c.Timeout = timeout);
                services.AddScoped<IChatService>(sp => sp.GetRequiredService<OpenAiChatService>());
                break;

            default:
                services.AddScoped<IChatService>(
                    sp => sp.GetRequiredKeyedService<IChatService>(FallbackKey));
                break;
        }

        return services;
    }
}
