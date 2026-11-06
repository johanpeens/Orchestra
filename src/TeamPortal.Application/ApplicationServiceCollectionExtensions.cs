using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Services;

namespace TeamPortal.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Picks up every AbstractValidator in this assembly. Called by both
    /// AddMockData() in Part A and AddSqlData() in Part B.
    /// </summary>
    public static IServiceCollection AddApplicationValidators(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ApplicationMarker>(ServiceLifetime.Singleton);

        // Built on the service interfaces rather than on a database, so one
        // implementation serves both halves of the seam. Registered here for the
        // same reason the validators are: both AddMockData and AddSqlData want it
        // and neither should have to remember.
        services.AddScoped<IPortalExportService, PortalExportService>();

        // The built-in retriever, as the DEFAULT. Which one actually answers is a
        // deployment question - retriever, Gemini or OpenAI - decided by
        // ChatRegistration.AddChat, which removes this and registers its own.
        // TryAdd rather than Add so the order of these two calls cannot matter.
        services.TryAddScoped<IChatService, PortalChatService>();

        return services;
    }
}

/// <summary>Anchor type for assembly scanning. Never instantiated.</summary>
public sealed class ApplicationMarker;
