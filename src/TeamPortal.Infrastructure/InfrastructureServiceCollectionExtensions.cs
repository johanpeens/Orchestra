using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application;
using TeamPortal.Application.Abstractions;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Email;
using TeamPortal.Infrastructure.Services;
using TeamPortal.Infrastructure.Storage;

namespace TeamPortal.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSqlData(
        this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        // AddDbContextFactory, never AddDbContext. A Blazor Server circuit lives
        // for hours; a scoped DbContext shared across concurrent renders throws
        // "A second operation was started on this context instance". Services
        // create and dispose a short-lived context per operation instead.
        // See Frontend/BuildPlan.md, "The one Blazor Server trap".
        // Scoped, not the default singleton. The context needs ICurrentUserService
        // to stamp the audit columns, and that is scoped - one scope is one
        // circuit. A singleton factory holding a scoped user is a captive
        // dependency, and BuildServiceProvider(validateScopes: true) says so.
        services.AddDbContextFactory<PortalDbContext>(
            options => options.UseSqlServer(connectionString),
            lifetime: ServiceLifetime.Scoped);

        return services.AddSqlServices();
    }

    /// <summary>
    /// Outgoing email over SMTP, and the base URL that links in those emails are
    /// built from. Both are configuration - see appsettings.json, and put the
    /// SMTP password in user secrets rather than next to it.
    /// </summary>
    public static IServiceCollection AddSmtpEmail(
        this IServiceCollection services, SmtpOptions options, string appBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IAppUrlProvider>(new AppUrlProvider(appBaseUrl));

        return services;
    }

    /// <summary>
    /// Uploads on the local filesystem. Phase B4 - replaces the in-memory store
    /// the app ran on through Part A. The root is outside wwwroot deliberately;
    /// see the note on LocalDiskFileStore.
    /// </summary>
    public static IServiceCollection AddLocalDiskFileStore(
        this IServiceCollection services, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new FileStoreOptions { RootPath = rootPath });
        services.AddSingleton<LocalDiskFileStore>();
        services.AddSingleton<IFileStore>(sp => sp.GetRequiredService<LocalDiskFileStore>());
        services.AddSingleton<IFileReader>(sp => sp.GetRequiredService<LocalDiskFileStore>());

        return services;
    }

    /// <summary>
    /// The service implementations on their own, with no opinion about which
    /// database is behind them. `AddSqlData` calls this after pointing the factory
    /// at SQL Server; the contract tests call it after pointing the factory at
    /// SQLite. Both then run exactly the same code.
    /// </summary>
    public static IServiceCollection AddSqlServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Shared with Part A - the validators were always in Application and did
        // not move when the storage did.
        services.AddApplicationValidators();

        // Phase B2. Each line here deletes a line from AddMockData().
        // Scoped, not singleton: in Blazor Server one scope is one circuit, so two
        // people using the app at once must not share an identity.
        services.AddScoped<IDepartmentService, SqlDepartmentService>();
        services.AddScoped<IEmployeeService, SqlEmployeeService>();
        services.AddScoped<IProcessFolderService, SqlProcessFolderService>();
        services.AddScoped<IProcessService, SqlProcessService>();
        services.AddScoped<IStepStatusService, SqlStepStatusService>();
        services.AddScoped<IActivityLogService, SqlActivityLogService>();
        services.AddScoped<INotificationService, SqlNotificationService>();
        services.AddScoped<IProcessStepService, SqlProcessStepService>();

        return services;
    }
}
