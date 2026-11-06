using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Dtos;
using TeamPortal.Mock;
using TeamPortal.Mock.Services;

namespace TeamPortal.Services.Tests.Common;

public sealed class MockServiceFixture : ServiceFixture
{
    private readonly Action<MockOptions>? _configure;

    public MockServiceFixture(Action<MockOptions>? configure = null)
    {
        _configure = configure;
        Build();
    }

    public override string DemoPassword => MockDataStoreConstants.DemoPassword;

    /// <summary>
    /// The simulated-latency and forced-failure switches. Only the mock has these,
    /// so only tests that are specifically about the mock may touch them.
    /// </summary>
    public MockOptions Options => Get<MockOptions>();

    /// <summary>Every email this fixture's services have sent.</summary>
    public MockEmailSender Email => Get<MockEmailSender>();

    /// <inheritdoc />
    public override void SignInAs(int userId, params string[] roles) =>
        Get<MockCurrentUserService>().SetUser(
            new CurrentUserDto(userId, "Test User", "test@company.com", null, roles));

    protected override void Register(IServiceCollection services) =>
        services.AddMockData(o =>
        {
            o.ReadDelay = TimeSpan.Zero;
            o.WriteDelay = TimeSpan.Zero;
            _configure?.Invoke(o);
        });
}

/// <summary>
/// Re-exposes the store's seeded password. Points at the real constant rather
/// than repeating the literal, so the two can never drift apart.
/// </summary>
public static class MockDataStoreConstants
{
    public const string DemoPassword = MockDataStore.DemoPassword;
}
