using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Web.Components.Layout;

public partial class UserMenu : ComponentBase
{
    [Inject]
    private ICurrentUserService CurrentUser { get; set; } = null!;

    [Inject]
    private IAuthService Auth { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <summary>
    /// Resolved optionally, NOT via [Inject]. A nullable annotation does not make
    /// an [Inject] property optional - Blazor still demands the service and throws
    /// "Cannot provide a value for property" when it is missing. Since the switcher
    /// is registered only in Development, [Inject] here would crash SIT, UAT and
    /// PROD on first render.
    /// </summary>
    private IDevRoleSwitcher? RoleSwitcher =>
        Services.GetService(typeof(IDevRoleSwitcher)) as IDevRoleSwitcher;

    /// <summary>
    /// Optional for the same reason as the role switcher above, and because the
    /// menu renders on every page including ones an admin never sees.
    /// </summary>
    private IUserApprovalService? Approvals =>
        Services.GetService(typeof(IUserApprovalService)) as IUserApprovalService;

    /// <summary>How many people are waiting on a decision. Zero hides the badge.</summary>
    private int PendingCount { get; set; }

    private CurrentUserDto? User => CurrentUser.User;

    private string DisplayName => User?.FullName ?? "Not signed in";

    private string RoleSummary =>
        User is null || User.Roles.Count == 0 ? "No role" : string.Join(", ", User.Roles);

    /// <summary>
    /// The role switcher exists so the UI can be checked as each role without
    /// four sets of credentials. It must never reach a deployed environment -
    /// phase B5 has a test asserting it is absent from Release builds.
    /// </summary>
    private bool ShowRoleSwitcher => RoleSwitcher is not null;

    /// <summary>
    /// The service returns 0 for anyone who is not an administrator, so this does
    /// not need its own role check - but it is still skipped for a signed-out
    /// visitor rather than making a pointless call on every anonymous page.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (Approvals is null || !CurrentUser.IsSignedIn)
        {
            PendingCount = 0;
            return;
        }

        PendingCount = await Approvals.GetPendingCountAsync();
    }

    private void SwitchRole(string role)
    {
        if (RoleSwitcher is null)
        {
            return;
        }

        RoleSwitcher.SwitchToRole(role);
        Navigation.Refresh(forceReload: false);
    }

    private async Task SignOutAsync()
    {
        await Auth.SignOutAsync();

        // Via the endpoint, because deleting the cookie needs a real response -
        // the same reason signing in does. forceLoad tears the circuit down too,
        // so nothing from the signed-in session survives.
        Navigation.NavigateTo("/account/sign-out", forceLoad: true);
    }
}
