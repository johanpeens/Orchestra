using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Web.Components.Dialogs;
using TeamPortal.Web.Components.Shared;

namespace TeamPortal.Web.Components.Layout;

public partial class NavMenu : ComponentBase
{
    private IReadOnlyList<ProcessFolderDto> _folders = [];
    private bool _loading = true;

    [Inject]
    private IProcessFolderService Folders { get; set; } = null!;

    [Inject]
    private IDialogService Dialogs { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Highlights the open folder. Set by the process page.</summary>
    [Parameter]
    public int? SelectedFolderId { get; set; }

    /// <summary>Raised by "New Process". Phase A8 opens the modal.</summary>
    [Parameter]
    public EventCallback OnNewProcess { get; set; }

    /// <summary>Raised by "View Process History". Phase A11 opens the panel.</summary>
    [Parameter]
    public EventCallback OnViewHistory { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _folders = await Folders.GetWithCountsAsync();
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task NewProcessAsync()
    {
        // The sidebar lists FOLDERS, so its action adds a folder. Processes are
        // added from the dashboard, where the flow strip shows them.
        if (await AddFolderDialog.ShowAsync(Dialogs))
        {
            Snackbar.Add("Folder added.", Severity.Success);
            await RefreshAsync();
        }

        await OnNewProcess.InvokeAsync();
    }

    private async Task ViewHistoryAsync()
    {
        await ActivityDialog.ShowActivityAsync(Dialogs);
        await OnViewHistory.InvokeAsync();
    }

    /// <summary>
    /// Deletes a folder and everything in it. The confirmation says exactly what
    /// that is - "7 processes and 12 steps" - because a confirmation that only
    /// asks "are you sure?" teaches people to click yes without reading.
    /// </summary>
    private async Task DeleteFolderAsync(ProcessFolderDto folder)
    {
        FolderContentsDto? contents = await Folders.GetContentsSummaryAsync(folder.Id);

        if (contents is null)
        {
            // Somebody else removed it while this sidebar was on screen.
            await RefreshAsync();
            return;
        }

        bool confirmed = await ConfirmDialog.ShowAsync(
            Dialogs,
            $"Delete the '{contents.Name}' folder?",
            "Delete folder",
            warning: contents.IsEmpty
                ? "The folder is empty, so nothing else goes with it."
                : $"This also deletes {contents.Summary} inside it. Nothing is left behind.",
            confirmText: contents.IsEmpty ? "Delete Folder" : "Delete Everything");

        if (!confirmed)
        {
            return;
        }

        Result result = contents.IsEmpty
            ? await Folders.DeleteAsync(folder.Id)
            : await Folders.DeleteWithEverythingInsideAsync(folder.Id);

        if (result.IsFailure)
        {
            Snackbar.Add(result.ErrorSummary, Severity.Error);
            return;
        }

        Snackbar.Add($"'{contents.Name}' deleted.", Severity.Success);

        await RefreshAsync();

        // The open folder just disappeared, so the page behind is showing rows
        // that no longer exist. Send them somewhere that does.
        if (SelectedFolderId == folder.Id)
        {
            Navigation.NavigateTo("/", forceLoad: false);
        }
    }

    /// <summary>Re-reads the counts. Called after a process is added or removed.</summary>
    public async Task RefreshAsync()
    {
        _folders = await Folders.GetWithCountsAsync();
        StateHasChanged();
    }
}
