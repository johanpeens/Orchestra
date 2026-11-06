using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Web.Components.Shared;

namespace TeamPortal.Web.Components.Dialogs;

public partial class EditDepartmentDialog : ComponentBase
{
    private string _name = string.Empty;
    private string _colour = BrandPalette.DefaultDepartmentColour;
    private bool _saving;
    private string? _formError;
    private string? _nameError;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private IDepartmentService Departments { get; set; } = null!;

    [Inject]
    private IDialogService Dialogs { get; set; } = null!;

    /// <summary>Null opens the dialog in "add" mode.</summary>
    [Parameter]
    public int? DepartmentId { get; set; }

    private bool IsNew => DepartmentId is null;

    private bool CanSave => !_saving && !string.IsNullOrWhiteSpace(_name);

    private string PreviewStyle => $"--preview-colour:{_colour};";

    private string Checked(string swatch) =>
        string.Equals(_colour, swatch, StringComparison.OrdinalIgnoreCase) ? "true" : "false";

    private string SwatchClass(string swatch) =>
        string.Equals(_colour, swatch, StringComparison.OrdinalIgnoreCase)
            ? "app-deptdialog__swatch app-deptdialog__swatch--selected"
            : "app-deptdialog__swatch";

    protected override async Task OnInitializedAsync()
    {
        if (DepartmentId is not int id)
        {
            return;
        }

        DepartmentDetailDto? detail = await Departments.GetByIdAsync(id);

        if (detail is null)
        {
            _formError = "That department could not be found. It may have been deleted.";
            return;
        }

        _name = detail.Name;
        _colour = detail.ColourHex;
    }

    private void PickColour(string colour)
    {
        _colour = colour;
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
            Result result = IsNew
                ? await Departments.CreateAsync(new CreateDepartmentRequest(_name.Trim(), _colour))
                : await Departments.UpdateAsync(
                    new UpdateDepartmentRequest(DepartmentId!.Value, _name.Trim(), _colour));

            if (result.IsFailure)
            {
                // The modal stays open and the message lands under the field it
                // belongs to. A snackbar alone would not show which box is wrong.
                ApplyErrors(result.Errors);
                return;
            }

            MudDialog.Close(DialogResult.Ok(DepartmentDialogOutcome.Saved));
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
            if (error.Field == nameof(UpdateDepartmentRequest.Name))
            {
                _nameError ??= error.Message;
            }
            else
            {
                _formError ??= error.Message;
            }
        }
    }

    /// <summary>
    /// Removes the department, once. The service refuses while anybody is still
    /// in it or any step is still tagged with it, and says which - so the
    /// confirmation here asks about intent and the service answers about
    /// consequence. Neither is worth doing twice.
    /// </summary>
    private async Task DeleteAsync()
    {
        if (_saving || DepartmentId is not { } id)
        {
            return;
        }

        ClearErrors();

        bool confirmed = await ConfirmDialog.ShowAsync(
            Dialogs,
            $"Delete the '{_name}' business unit?",
            "Delete business unit",
            warning: "Anyone in it, and any process step tagged with it, has to be "
                   + "moved first - this will refuse rather than orphan them.",
            confirmText: "Delete");

        if (!confirmed)
        {
            return;
        }

        _saving = true;

        try
        {
            Result result = await Departments.DeleteAsync(id);

            if (result.IsFailure)
            {
                // Straight onto the form. The service's message names what is
                // still in the way, which is the only useful thing to say next.
                ApplyErrors(result.Errors);
                return;
            }

            MudDialog.Close(DialogResult.Ok(DepartmentDialogOutcome.Deleted));
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();

    /// <summary>
    /// Opens the dialog and says what happened. Not a bool: the caller announces
    /// the outcome, and "Department updated" after a delete names a row that is
    /// no longer there.
    /// </summary>
    public static async Task<DepartmentDialogOutcome> ShowAsync(
        IDialogService dialogs, int? departmentId = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<EditDepartmentDialog>
        {
            { x => x.DepartmentId, departmentId }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog = await dialogs.ShowAsync<EditDepartmentDialog>(
            departmentId is null ? "Add Department" : "Edit Department", parameters, options);

        DialogResult? result = await dialog.Result;

        return result is { Canceled: false, Data: DepartmentDialogOutcome outcome }
            ? outcome
            : DepartmentDialogOutcome.Cancelled;
    }
}

/// <summary>What the Edit Department dialog did.</summary>
public enum DepartmentDialogOutcome
{
    /// <summary>Closed without changing anything.</summary>
    Cancelled = 0,
    Saved = 1,
    Deleted = 2
}
