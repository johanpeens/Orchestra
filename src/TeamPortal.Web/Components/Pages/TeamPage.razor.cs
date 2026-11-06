using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Web.Components.Dialogs;
using TeamPortal.Web.Components.Shared;

namespace TeamPortal.Web.Components.Pages;

public partial class TeamPage : ComponentBase
{
    private IReadOnlyList<DepartmentListItemDto> _departments = [];
    private IReadOnlyList<EmployeeCardDto> _employees = [];
    private int? _selectedDepartmentId;
    private string _search = string.Empty;
    private bool _loading = true;

    /// <summary>
    /// Guards against an out-of-order response overwriting a newer one. Two
    /// searches typed quickly can return in either order, and without this the
    /// slower, older result wins and the grid shows the wrong thing.
    /// </summary>
    private int _requestId;

    [Inject]
    private IEmployeeService Employees { get; set; } = null!;

    [Inject]
    private IDepartmentService Departments { get; set; } = null!;

    [Inject]
    private IDialogService Dialogs { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private bool IsSearching => !string.IsNullOrWhiteSpace(_search);

    private string EmptyTitle =>
        IsSearching ? "No one matches that search" : "No people in this department yet";

    private string EmptyDescription =>
        IsSearching
            ? "Try a different name, role or department."
            : "Add someone to this department to see them here.";

    protected override async Task OnInitializedAsync()
    {
        _departments = await Departments.GetWithCountsAsync();
        await ReloadEmployeesAsync();
    }

    private async Task OnDepartmentSelectedAsync(int? departmentId)
    {
        _selectedDepartmentId = departmentId;

        // Picking a department is an explicit choice; a stale search term on top
        // of it would silently hide most of the result.
        _search = string.Empty;

        await ReloadEmployeesAsync();
    }

    private async Task OnSearchChangedAsync(string? term)
    {
        _search = term ?? string.Empty;

        // Searching is global by design - the placeholder says "name, role or
        // department", so scoping it to the current filter would contradict it.
        if (IsSearching)
        {
            _selectedDepartmentId = null;
        }

        await ReloadEmployeesAsync();
    }

    private async Task AddEmployeeAsync()
    {
        bool saved = await AddEmployeeDialog.ShowAsync(
            Dialogs, defaultDepartmentId: _selectedDepartmentId);

        if (saved)
        {
            Snackbar.Add("Employee added.", Severity.Success);
            await ReloadAllAsync();
        }
    }

    private Task ViewActivityAsync() => ActivityDialog.ShowActivityAsync(Dialogs);

    private async Task EditEmployeeAsync(int employeeId)
    {
        if (await AddEmployeeDialog.ShowAsync(Dialogs, employeeId))
        {
            Snackbar.Add("Employee saved.", Severity.Success);
            await ReloadAllAsync();
        }
    }

    private async Task EditDepartmentAsync(int departmentId)
    {
        DepartmentDialogOutcome outcome =
            await EditDepartmentDialog.ShowAsync(Dialogs, departmentId);

        if (outcome is DepartmentDialogOutcome.Cancelled)
        {
            return;
        }

        Snackbar.Add(
            outcome is DepartmentDialogOutcome.Deleted
                ? "Business unit deleted."
                : "Department updated.",
            Severity.Success);

        // Deleting the unit you were filtered by would otherwise leave the list
        // asking for an id that no longer exists - an empty page with nothing
        // on screen highlighted to explain why. Back to All.
        if (outcome is DepartmentDialogOutcome.Deleted && _selectedDepartmentId == departmentId)
        {
            _selectedDepartmentId = null;
        }

        await ReloadAllAsync();
    }

    /// <summary>
    /// Re-reads the counts as well as the cards. A new employee changes both, and
    /// a department rename or recolour changes every card that shows it.
    /// </summary>
    private async Task ReloadAllAsync()
    {
        _departments = await Departments.GetWithCountsAsync();
        await ReloadEmployeesAsync();
    }

    private async Task ReloadEmployeesAsync()
    {
        int request = ++_requestId;
        _loading = true;
        StateHasChanged();

        try
        {
            IReadOnlyList<EmployeeCardDto> result = IsSearching
                ? await Employees.SearchAsync(_search)
                : _selectedDepartmentId is int id
                    ? await Employees.GetByDepartmentAsync(id)
                    : await Employees.GetAllAsync();

            // A newer request started while this one was in flight - drop this.
            if (request != _requestId)
            {
                return;
            }

            _employees = result;
        }
        finally
        {
            if (request == _requestId)
            {
                _loading = false;
            }
        }
    }
}
