using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Requests;
using TeamPortal.Mock;
using TeamPortal.Web.Components.Layout;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Shell;

public class UserMenuTests : ComponentTestBase
{
    [Fact]
    public void UserMenu_WhenNotSignedIn_StillRenders()
    {
        IRenderedComponent<UserMenu> cut = Render<UserMenu>();

        cut.Markup.Should().Contain("Not signed in");
    }

    [Fact]
    public async Task UserMenu_WhenSignedIn_ShowsTheUsersName()
    {
        IAuthService auth = Services.GetRequiredService<IAuthService>();
        await auth.SignInAsync(new SignInRequest("admin@company.com", "Portal!2026"));

        IRenderedComponent<UserMenu> cut = Render<UserMenu>();

        cut.Markup.Should().Contain("Admin User");
    }

    [Fact]
    public void UserMenu_HasAnAccessibleNameOnItsIconButton()
    {
        IRenderedComponent<UserMenu> cut = Render<UserMenu>();

        cut.Find("button").GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
    }
}

/// <summary>
/// Renders the account menu WITHOUT IDevRoleSwitcher registered - the shape of
/// every environment above Development.
/// </summary>
public class UserMenuWithoutRoleSwitcherTests : BunitContext, IAsyncLifetime
{
    public UserMenuWithoutRoleSwitcherTests()
    {
        Services.AddMudServices();

        // EnableDevRoleSwitcher defaults to false, so IDevRoleSwitcher is absent.
        Services.AddMockData(o =>
        {
            o.ReadDelay = TimeSpan.Zero;
            o.WriteDelay = TimeSpan.Zero;
        });

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void UserMenu_WithoutTheRoleSwitcherRegistered_StillRenders()
    {
        // Regression. This threw "Cannot provide a value for property 'RoleSwitcher'"
        // in every environment except Development, because a nullable annotation
        // does NOT make an [Inject] property optional - Blazor still demands the
        // service. The switcher is resolved through IServiceProvider instead.
        Action act = () => Render<UserMenu>();

        act.Should().NotThrow();
    }

    [Fact]
    public void UserMenu_WithoutTheRoleSwitcher_HidesTheViewAsSection()
    {
        IRenderedComponent<UserMenu> cut = Render<UserMenu>();

        cut.Markup.Should().NotContain("View as",
            "the dev-only switcher must be invisible outside Development");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}

/// <summary>Renders the account menu WITH the switcher, as in Development.</summary>
public class UserMenuWithRoleSwitcherTests : BunitContext, IAsyncLifetime
{
    public UserMenuWithRoleSwitcherTests()
    {
        Services.AddMudServices();

        Services.AddMockData(o =>
        {
            o.ReadDelay = TimeSpan.Zero;
            o.WriteDelay = TimeSpan.Zero;
            o.EnableDevRoleSwitcher = true;
        });

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void UserMenu_WithTheRoleSwitcher_IsRegistered()
    {
        Services.GetService<IDevRoleSwitcher>().Should().NotBeNull();
    }

    [Fact]
    public void UserMenu_WithTheRoleSwitcher_Renders()
    {
        Action act = () => Render<UserMenu>();

        act.Should().NotThrow();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}
