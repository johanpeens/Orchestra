using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TeamPortal.Web.Components.Shared;

public partial class ConfirmDialog : ComponentBase
{
    private bool _working;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Title { get; set; } = "Are you sure?";

    [Parameter]
    [EditorRequired]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional extra line for anything the user cannot undo.</summary>
    [Parameter]
    public string? Warning { get; set; }

    [Parameter]
    public string ConfirmText { get; set; } = "Delete";

    [Parameter]
    public string CancelText { get; set; } = "Cancel";

    /// <summary>Red by default. Pass Color.Primary for a non-destructive confirm.</summary>
    [Parameter]
    public Color ConfirmColour { get; set; } = Color.Error;

    private void Confirm()
    {
        // Guard against a double-click producing two deletes. The dialog closes
        // immediately, but the click handler can still fire twice on a slow circuit.
        if (_working)
        {
            return;
        }

        _working = true;
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog.Cancel();

    /// <summary>
    /// Opens the dialog and returns true only if the user confirmed. Cancelling,
    /// pressing Escape and clicking the backdrop all return false.
    /// </summary>
    public static async Task<bool> ShowAsync(
        IDialogService dialogs,
        string message,
        string title = "Are you sure?",
        string? warning = null,
        string confirmText = "Delete",
        Color confirmColour = Color.Error)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Title, title },
            { x => x.Message, message },
            { x => x.Warning, warning },
            { x => x.ConfirmText, confirmText },
            { x => x.ConfirmColour, confirmColour }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog = await dialogs.ShowAsync<ConfirmDialog>(title, parameters, options);
        DialogResult? result = await dialog.Result;

        return result is { Canceled: false, Data: true };
    }
}
