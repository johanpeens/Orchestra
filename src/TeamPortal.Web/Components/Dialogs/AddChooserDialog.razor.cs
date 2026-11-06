using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TeamPortal.Web.Components.Dialogs;

public partial class AddChooserDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>Named in the "a step" choice, so it says WHERE the step lands.</summary>
    [Parameter]
    public string ProcessName { get; set; } = "this process";

    /// <summary>Named in the "a process" choice, for the same reason.</summary>
    [Parameter]
    public string FolderName { get; set; } = "this folder";

    private void Choose(AddChoice choice) => MudDialog.Close(DialogResult.Ok(choice));

    private void Cancel() => MudDialog.Cancel();

    /// <summary>
    /// Asks what to add. Returns <see cref="AddChoice.Nothing"/> when the
    /// question is dismissed.
    /// </summary>
    public static async Task<AddChoice> ShowAsync(
        IDialogService dialogs, string processName, string folderName)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<AddChooserDialog>
        {
            { x => x.ProcessName, processName },
            { x => x.FolderName, folderName }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseButton = true,
            BackdropClick = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog =
            await dialogs.ShowAsync<AddChooserDialog>("Add", parameters, options);

        DialogResult? result = await dialog.Result;

        return result is { Canceled: false, Data: AddChoice choice }
            ? choice
            : AddChoice.Nothing;
    }
}

public enum AddChoice
{
    Nothing = 0,
    Step = 1,
    Process = 2
}
