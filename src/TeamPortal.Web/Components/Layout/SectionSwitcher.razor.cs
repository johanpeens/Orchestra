using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace TeamPortal.Web.Components.Layout;

public partial class SectionSwitcher : ComponentBase, IDisposable
{
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private AppSection Section => AppSections.FromPath(
        Navigation.ToBaseRelativePath(Navigation.Uri));

    private string PillClass(AppSection section) =>
        section == Section ? "app-pill app-pill--active" : "app-pill";

    private string? Current(AppSection section) => section == Section ? "page" : null;

    protected override void OnInitialized() =>
        Navigation.LocationChanged += OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
        InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        GC.SuppressFinalize(this);
    }
}
