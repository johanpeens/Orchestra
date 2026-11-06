using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Web.Components.Shared;

namespace TeamPortal.Web.Components.Dialogs;

public partial class ProcessDialog : ComponentBase
{
    private IReadOnlyList<DepartmentListItemDto> _units = [];
    private string _name = string.Empty;
    private string _description = string.Empty;
    private int? _departmentId;
    private bool _saving;
    private string? _formError;
    private string? _nameError;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private IProcessService Processes { get; set; } = null!;

    [Inject]
    private IDepartmentService Departments { get; set; } = null!;

    [Inject]
    private IDialogService Dialogs { get; set; } = null!;

    /// <summary>Null opens the dialog in "add" mode.</summary>
    [Parameter]
    public int? ProcessId { get; set; }

    /// <summary>Which folder a new process lands in. Required when adding.</summary>
    [Parameter]
    public int FolderId { get; set; }

    private bool IsNew => ProcessId is null;

    private bool CanSave => !_saving && !string.IsNullOrWhiteSpace(_name);

    private string UnitClass(int id) =>
        _departmentId == id
            ? "app-procdialog__unit app-procdialog__unit--selected"
            : "app-procdialog__unit";

    protected override async Task OnInitializedAsync()
    {
        _units = await Departments.GetWithCountsAsync();

        if (ProcessId is not int id)
        {
            return;
        }

        ProcessDetailDto? detail = await Processes.GetDetailAsync(id);

        if (detail is null)
        {
            _formError = "That process could not be found. It may have been deleted.";
            return;
        }

        _name = detail.Name;
        _description = detail.Description ?? string.Empty;
        _departmentId = detail.DepartmentId;
    }

    private void PickUnit(int id)
    {
        // Clicking the selected unit again clears it - the field is optional and
        // there would otherwise be no way to unset it.
        _departmentId = _departmentId == id ? null : id;
        ClearErrors();
    }

    private void ClearErrors()
    {
        _formError = null;
        _nameError = null;
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
            string? description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim();

            Result result = IsNew
                ? await Processes.CreateAsync(
                    new CreateProcessRequest(
                        _name.Trim(), FolderId, description, DepartmentId: _departmentId))
                : await Processes.UpdateAsync(
                    new UpdateProcessRequest(
                        ProcessId!.Value, _name.Trim(), description, DepartmentId: _departmentId));

            if (result.IsFailure)
            {
                foreach (ValidationError error in result.Errors)
                {
                    if (error.Field == nameof(CreateProcessRequest.Name))
                    {
                        _nameError ??= error.Message;
                    }
                    else
                    {
                        _formError ??= error.Message;
                    }
                }

                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (ProcessId is not int id)
        {
            return;
        }

        // Always confirm first. Deleting a process takes its steps with it.
        bool confirmed = await ConfirmDialog.ShowAsync(
            Dialogs,
            $"Delete '{_name}'?",
            "Delete process",
            warning: "Every step in this process will be removed with it.",
            confirmText: "Delete Process");

        if (!confirmed)
        {
            return;
        }

        _saving = true;

        try
        {
            Result result = await Processes.DeleteAsync(id);

            if (result.IsFailure)
            {
                _formError = result.ErrorSummary;
                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();

    /// <summary>Opens the dialog. Returns true when something was saved or deleted.</summary>
    public static async Task<bool> ShowAsync(
        IDialogService dialogs, int folderId, int? processId = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<ProcessDialog>
        {
            { x => x.FolderId, folderId },
            { x => x.ProcessId, processId }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog = await dialogs.ShowAsync<ProcessDialog>(
            processId is null ? "Add Process" : "Edit Process", parameters, options);

        DialogResult? result = await dialog.Result;

        return result is { Canceled: false, Data: true };
    }
}
