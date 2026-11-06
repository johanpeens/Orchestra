using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TeamPortal.Web.Components.Shared;

public partial class ActivityDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Title { get; set; } = "Activity Log";

    [Parameter]
    public string? EntityName { get; set; }

    [Parameter]
    public int? EntityId { get; set; }

    private void Close() => MudDialog.Close();

    /// <summary>Opens the whole-app log.</summary>
    public static Task ShowActivityAsync(IDialogService dialogs) =>
        ShowAsync(dialogs, "Activity Log", null, null);

    /// <summary>Opens the history for one record.</summary>
    public static Task ShowHistoryAsync(IDialogService dialogs, string entityName, int entityId) =>
        ShowAsync(dialogs, "Process History", entityName, entityId);

    private static async Task ShowAsync(
        IDialogService dialogs, string title, string? entityName, int? entityId)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<ActivityDialog>
        {
            { x => x.Title, title },
            { x => x.EntityName, entityName },
            { x => x.EntityId, entityId }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog = await dialogs.ShowAsync<ActivityDialog>(title, parameters, options);
        await dialog.Result;
    }
}
