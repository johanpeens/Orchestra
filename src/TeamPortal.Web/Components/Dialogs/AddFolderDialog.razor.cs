using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Requests;

namespace TeamPortal.Web.Components.Dialogs;

public partial class AddFolderDialog : ComponentBase
{
    private string _name = string.Empty;
    private bool _saving;
    private string? _formError;
    private string? _nameError;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private IProcessFolderService Folders { get; set; } = null!;

    private bool CanSave => !_saving && !string.IsNullOrWhiteSpace(_name);

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
            Result result = await Folders.CreateAsync(new CreateProcessFolderRequest(_name.Trim()));

            if (result.IsFailure)
            {
                foreach (ValidationError error in result.Errors)
                {
                    if (error.Field == nameof(CreateProcessFolderRequest.Name))
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

    private void Cancel() => MudDialog.Cancel();

    /// <summary>Opens the dialog. Returns true when a folder was created.</summary>
    public static async Task<bool> ShowAsync(IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog = await dialogs.ShowAsync<AddFolderDialog>("Add Folder", options);
        DialogResult? result = await dialog.Result;

        return result is { Canceled: false, Data: true };
    }
}
