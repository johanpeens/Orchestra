using Microsoft.Extensions.DependencyInjection;

namespace TeamPortal.Infrastructure.Messaging;

public static class MessagingRegistration
{
    /// <summary>
    /// Registers the WhatsApp channel. With WhatsApp:Enabled off - which is the
    /// default - nothing is registered and the webhook refuses everything, so a
    /// deployment that has not been configured for it cannot accidentally start
    /// answering strangers.
    /// </summary>
    public static IServiceCollection AddWhatsApp(
        this IServiceCollection services, WhatsAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);

        if (!options.IsUsable)
        {
            return services;
        }

        services.AddMemoryCache();
        services.AddSingleton<IWhatsAppConversations, WhatsAppConversations>();

        services.AddHttpClient<IWhatsAppSender, TwilioWhatsAppSender>(
            c => c.Timeout = TimeSpan.FromSeconds(20));

        return services;
    }
}
