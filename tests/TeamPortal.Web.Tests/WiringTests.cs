using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TeamPortal.Application.Requests;
using TeamPortal.Mock;

namespace TeamPortal.Web.Tests;

public class WiringTests
{
    [Fact]
    public void TestInfrastructure_IsWiredUp()
    {
        true.Should().BeTrue();
    }

    [Fact]
    public void AddMockData_BuildsWithoutThrowing()
    {
        var services = new ServiceCollection();

        services.AddMockData();
        ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddMockData_RegistersTheSharedValidators()
    {
        // Validators live in Application and are shared with Part B. If assembly
        // scanning stops finding them, every form silently accepts anything.
        var services = new ServiceCollection();
        services.AddMockData();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IValidator<CreateEmployeeRequest>>().Should().NotBeNull();
        provider.GetService<IValidator<CreateStepRequest>>().Should().NotBeNull();
        provider.GetService<IValidator<SignInRequest>>().Should().NotBeNull();
        provider.GetService<IValidator<UpdateDepartmentRequest>>().Should().NotBeNull();
    }

    [Fact]
    public void AddMockData_AppliesTheSuppliedOptions()
    {
        var services = new ServiceCollection();
        services.AddMockData(o =>
        {
            o.ReadDelay = TimeSpan.Zero;
            o.WriteDelay = TimeSpan.Zero;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        MockOptions options = provider.GetRequiredService<MockOptions>();

        options.ReadDelay.Should().Be(TimeSpan.Zero);
        options.WriteDelay.Should().Be(TimeSpan.Zero);
    }
}
