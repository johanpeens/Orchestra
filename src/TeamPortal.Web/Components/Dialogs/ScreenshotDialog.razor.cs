using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TeamPortal.Web.Components.Dialogs;

public partial class ScreenshotDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string Url { get; set; } = string.Empty;

    [Parameter]
    public string FileName { get; set; } = "Screenshot";

    public static async Task ShowAsync(IDialogService dialogs, string url, string fileName)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<ScreenshotDialog>
        {
            { x => x.Url, url },
            { x => x.FileName, fileName }
        };

        var options = new DialogOptions
        {
            // Full screen, because the point is to see the thing at its own size.
            FullScreen = true,
            CloseButton = true,
            BackdropClick = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog =
            await dialogs.ShowAsync<ScreenshotDialog>(fileName, parameters, options);

        await dialog.Result;
    }

    private void Close() => MudDialog.Close();
}
