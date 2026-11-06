using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application;
using TeamPortal.Application.Abstractions;
using TeamPortal.Mock.Services;

namespace TeamPortal.Mock;

public static class MockServiceCollectionExtensions
{
    /// <summary>
    /// Wires up the mock data layer. Called from Program.cs and from the bUnit
    /// tests, so both run against exactly the same registrations.
    /// </summary>
    public static IServiceCollection AddMockData(this IServiceCollection services) =>
        services.AddMockData(_ => { });

    /// <inheritdoc cref="AddMockData(IServiceCollection)"/>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Adjusts the simulated read/write delays and the failure switch. Tests set the
    /// delays to zero; the running app leaves them at their defaults so the UI is
    /// built against latency rather than against instant answers.
    /// </param>
    public static IServiceCollection AddMockData(
        this IServiceCollection services, Action<MockOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MockOptions();
        configure(options);
        services.AddSingleton(options);

        // Validators are shared with Part B - written once, in Application.
        services.AddApplicationValidators();

        // One "database" for the whole app.
        services.AddSingleton<MockDataStore>();

        // Scoped, not singleton: in Blazor Server one scope is one circuit, so
        // two people using the app at once must not share an identity.
        services.AddScoped<MockCurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<MockCurrentUserService>());

        services.AddScoped<MockAuthService>();
        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<MockAuthService>());

        if (options.EnableDevRoleSwitcher)
        {
            services.AddScoped<IDevRoleSwitcher>(sp => sp.GetRequiredService<MockAuthService>());
        }

        services.AddSingleton<IFileStore, MockFileStore>();

        // Nothing leaves the building in Part A. The sender keeps every message
        // so a test can read the confirmation link out of it.
        services.AddSingleton<MockEmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<MockEmailSender>());
        services.AddSingleton<IAppUrlProvider>(new MockAppUrlProvider());

        // Stateless over the singleton store, but scoped so they can depend on
        // ICurrentUserService without a captive-dependency problem.
        services.AddScoped<IDepartmentService, MockDepartmentService>();
        services.AddScoped<IEmployeeService, MockEmployeeService>();
        services.AddScoped<IProcessFolderService, MockProcessFolderService>();
        services.AddScoped<IProcessService, MockProcessService>();
        services.AddScoped<IProcessStepService, MockProcessStepService>();
        services.AddScoped<IStepStatusService, MockStepStatusService>();
        services.AddScoped<IActivityLogService, MockActivityLogService>();
        services.AddScoped<INotificationService, MockNotificationService>();
        services.AddScoped<IUserApprovalService, MockUserApprovalService>();

        return services;
    }
}

/// <summary>
/// Stands in for the configured base URL. A fixed host, so a test asserting on a
/// link does not have to know how the app is hosted.
/// </summary>
public sealed class MockAppUrlProvider : IAppUrlProvider
{
    public string BaseUrl => "https://localhost";

    public string Absolute(string relativePath, params (string Key, string Value)[] query)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(query);

        string url = $"{BaseUrl}/{relativePath.TrimStart('/')}";
        char separator = '?';

        foreach ((string key, string value) in query)
        {
            url += $"{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
            separator = '&';
        }

        return url;
    }
}
