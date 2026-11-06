using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;

namespace TeamPortal.Services.Tests.Common;

public abstract class ServiceFixture : IDisposable
{
    private ServiceProvider? _root;
    private IServiceScope? _scope;

    /// <summary>
    /// The password the seeded demo accounts use. Both sides seed the same one -
    /// each points at its own constant rather than repeating the literal.
    /// </summary>
    public abstract string DemoPassword { get; }

    public T Get<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();

    public IEmployeeService Employees => Get<IEmployeeService>();

    public IDepartmentService Departments => Get<IDepartmentService>();

    public IProcessService Processes => Get<IProcessService>();

    public IProcessFolderService Folders => Get<IProcessFolderService>();

    public IProcessStepService Steps => Get<IProcessStepService>();

    public IStepStatusService Statuses => Get<IStepStatusService>();

    public IActivityLogService ActivityLog => Get<IActivityLogService>();

    public INotificationService Notifications => Get<INotificationService>();

    public IAuthService Auth => Get<IAuthService>();

    public IUserApprovalService Approvals => Get<IUserApprovalService>();

    public IFileStore Files => Get<IFileStore>();

    private IServiceScope Scope => _scope ?? throw new InvalidOperationException(
        "The fixture was used before Build() ran. Call it from the concrete constructor.");

    /// <summary>
    /// Puts an identity in place without going through a password. Both sides
    /// need it - the approval tests are about what a role may do, not about
    /// signing in - and each has its own way of holding the current user.
    /// </summary>
    public abstract void SignInAs(int userId, params string[] roles);

    /// <summary>Registers one side of the seam. The only thing that differs.</summary>
    protected abstract void Register(IServiceCollection services);

    /// <summary>
    /// Anything the graph needs before the first test line runs - for EF Core,
    /// creating and seeding the database. A no-op for the mock, which seeds itself
    /// in its constructor.
    /// </summary>
    protected virtual void Prepare()
    {
    }

    /// <summary>
    /// Called from the concrete constructor rather than from this one, so that a
    /// subclass can finish setting up its own fields - a SQLite connection, say -
    /// before <see cref="Register"/> is handed them.
    /// </summary>
    protected void Build()
    {
        var services = new ServiceCollection();
        Register(services);

        _root = services.BuildServiceProvider(validateScopes: true);
        _scope = _root.CreateScope();

        Prepare();
    }

    /// <summary>Signs in as the seeded admin, for tests that need an identity.</summary>
    public async Task SignInAsAdminAsync()
    {
        SignInResultDto result = await Auth.SignInAsync(
            new SignInRequest("admin@company.com", DemoPassword));

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("The seeded admin could not sign in.");
        }
    }

    /// <summary>The id of the seeded "Business Enablement" department.</summary>
    public async Task<int> EnablementDepartmentIdAsync()
    {
        IReadOnlyList<DepartmentListItemDto> all = await Departments.GetWithCountsAsync();
        return all.First(d => d.Name == "Business Enablement").Id;
    }

    /// <summary>The id of the seeded "Client Onboarding" folder.</summary>
    public async Task<int> OnboardingFolderIdAsync()
    {
        IReadOnlyList<ProcessFolderDto> folders = await Folders.GetWithCountsAsync();
        return folders.First(f => f.Name == "Client Onboarding").Id;
    }

    /// <summary>
    /// The id of "Initial Contact" - the first process in the flow strip, and the
    /// only seeded process that has steps written up.
    /// </summary>
    public async Task<int> FirstProcessIdAsync()
    {
        IReadOnlyList<ProcessListItemDto> processes =
            await Processes.GetByFolderAsync(await OnboardingFolderIdAsync());

        return processes.First(p => p.Name == "Initial Contact").Id;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _scope?.Dispose();
        _root?.Dispose();
    }
}
