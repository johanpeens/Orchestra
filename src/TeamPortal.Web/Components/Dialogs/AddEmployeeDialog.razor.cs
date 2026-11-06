using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;

namespace TeamPortal.Web.Components.Dialogs;

public partial class AddEmployeeDialog : ComponentBase
{
    private IReadOnlyList<DepartmentListItemDto> _departments = [];
    private string _name = string.Empty;
    private string _jobTitle = string.Empty;
    private int _departmentId;
    private StaffType _staffType = StaffType.Employee;
    private bool _saving;
    private string? _formError;
    private string? _nameError;
    private string? _departmentError;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private IEmployeeService Employees { get; set; } = null!;

    [Inject]
    private IDepartmentService Departments { get; set; } = null!;

    /// <summary>Null opens the dialog in "add" mode.</summary>
    [Parameter]
    public int? EmployeeId { get; set; }

    /// <summary>Preselected department, e.g. the one currently being filtered on.</summary>
    [Parameter]
    public int? DefaultDepartmentId { get; set; }

    private bool IsNew => EmployeeId is null;

    private bool CanSave => !_saving && !string.IsNullOrWhiteSpace(_name) && _departmentId > 0;

    protected override async Task OnInitializedAsync()
    {
        _departments = await Departments.GetWithCountsAsync();

        if (EmployeeId is int id)
        {
            EmployeeDetailDto? detail = await Employees.GetByIdAsync(id);

            if (detail is null)
            {
                _formError = "That employee could not be found. They may have been removed.";
                return;
            }

            _name = detail.FullName;
            _jobTitle = detail.JobTitle ?? string.Empty;
            _departmentId = detail.DepartmentId;
            _staffType = detail.StaffType;
            return;
        }

        _departmentId = DefaultDepartmentId ?? (_departments.Count > 0 ? _departments[0].Id : 0);
    }

    private void ClearErrors()
    {
        _formError = null;
        _nameError = null;
        _departmentError = null;
    }

    private async Task SaveAsync()
    {
        if (_saving)
        {
            return;
        }

        ClearErrors();
        _saving = true;

        try
        {
            string? title = string.IsNullOrWhiteSpace(_jobTitle) ? null : _jobTitle.Trim();

            Result result = IsNew
                ? await Employees.CreateAsync(
                    new CreateEmployeeRequest(_name.Trim(), _departmentId, title, StaffType: _staffType))
                : await Employees.UpdateAsync(
                    new UpdateEmployeeRequest(
                        EmployeeId!.Value, _name.Trim(), _departmentId, title, StaffType: _staffType));

            if (result.IsFailure)
            {
                ApplyErrors(result.Errors);
                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            _saving = false;
        }
    }

    private void ApplyErrors(IReadOnlyList<ValidationError> errors)
    {
        foreach (ValidationError error in errors)
        {
            if (error.Field == nameof(CreateEmployeeRequest.FullName))
            {
                _nameError ??= error.Message;
            }
            else if (error.Field == nameof(CreateEmployeeRequest.DepartmentId))
            {
                _departmentError ??= error.Message;
            }
            else
            {
                _formError ??= error.Message;
            }
        }
    }

    private void Cancel() => MudDialog.Cancel();

    /// <summary>Opens the dialog. Returns true when something was saved.</summary>
    public static async Task<bool> ShowAsync(
        IDialogService dialogs, int? employeeId = null, int? defaultDepartmentId = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<AddEmployeeDialog>
        {
            { x => x.EmployeeId, employeeId },
            { x => x.DefaultDepartmentId, defaultDepartmentId }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog = await dialogs.ShowAsync<AddEmployeeDialog>(
            employeeId is null ? "Add Employee" : "Edit Employee", parameters, options);

        DialogResult? result = await dialog.Result;

        return result is { Canceled: false, Data: true };
    }
}
