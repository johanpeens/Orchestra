using Microsoft.AspNetCore.Components;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Web.Components.Shared;

public partial class DepartmentFilter : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<DepartmentListItemDto> Departments { get; set; } = [];

    /// <summary>Null means the "All" row is active.</summary>
    [Parameter]
    public int? SelectedDepartmentId { get; set; }

    [Parameter]
    public EventCallback<int?> SelectedDepartmentIdChanged { get; set; }

    /// <summary>
    /// Bound by phase A6 to open the Edit Department modal. While it is unbound
    /// the pencil is not rendered at all - an inert button is worse than none.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnEdit { get; set; }

    /// <summary>
    /// The "All" count. Summed from the rows rather than fetched separately, so
    /// it can never disagree with the numbers beside it.
    /// </summary>
    private int TotalCount => Departments.Sum(d => d.EmployeeCount);

    /// <summary>
    /// Blazor renders a bool attribute value as presence or absence, not as the
    /// text "true", and aria-pressed needs the literal string to mean anything.
    /// </summary>
    private string Pressed(int? id) => (SelectedDepartmentId == id) ? "true" : "false";

    private string RowClass(int? id) =>
        SelectedDepartmentId == id
            ? "app-deptfilter__row app-deptfilter__row--selected"
            : "app-deptfilter__row";

    private Task SelectAsync(int? id) => SelectedDepartmentIdChanged.InvokeAsync(id);
}
