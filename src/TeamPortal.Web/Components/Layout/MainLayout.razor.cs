using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

namespace TeamPortal.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    /// <summary>
    /// Collapsed on arrival. The folder rail is the widest thing on the page and
    /// is rarely what somebody came to look at; the toggle in the app bar brings
    /// it back.
    /// </summary>
    private bool _drawerOpen;
    private ErrorBoundary? _errorBoundary;
    private NavMenu? _navMenu;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private bool HasFolderRail =>
        AppSections.FromPath(Navigation.ToBaseRelativePath(Navigation.Uri)).HasFolderRail();

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    /// <summary>
    /// Clears the error boundary so the page re-renders. Without this the
    /// boundary latches and "Try again" would do nothing.
    /// </summary>
    private async Task RecoverAsync()
    {
        _errorBoundary?.Recover();

        if (_navMenu is not null)
        {
            await _navMenu.RefreshAsync();
        }
    }

    protected override void OnInitialized() =>
        Navigation.LocationChanged += OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
        InvokeAsync(StateHasChanged);

    protected override void OnParametersSet()
    {
        // A navigation is a fresh start - do not carry a previous page's error
        // into the next one.
        _errorBoundary?.Recover();
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }
}
