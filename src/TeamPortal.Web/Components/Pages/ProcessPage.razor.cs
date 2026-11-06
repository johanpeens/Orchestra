using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Web.Components.Dialogs;
using TeamPortal.Web.Components.Shared;

namespace TeamPortal.Web.Components.Pages;

public partial class ProcessPage : ComponentBase
{
    private IReadOnlyList<ProcessListItemDto> _processes = [];
    private IReadOnlyList<ProcessStepDto> _steps = [];

    /// <summary>
    /// One entry per process somebody has pointed at. Cached because a strip of
    /// seven should not cost seven round trips before anyone hovers anything,
    /// and a second hover over the same card should cost nothing at all.
    /// </summary>
    private readonly Dictionary<int, ProcessPreview> _previews = [];

    /// <summary>Guards against a re-entrant load while the mouse sits still.</summary>
    private readonly HashSet<int> _previewsLoading = [];

    /// <summary>The card being pointed at, or focused. Null hides the band.</summary>
    private int? _hoveredProcessId;

    /// <summary>
    /// Whether the PROCESS FLOW strip is showing. Off by default: it is the
    /// tallest thing on the page, and once you are reading one process's steps
    /// it is seven other cards in the way. The legend takes its place while it
    /// is closed - see <see cref="DepartmentLegend"/>.
    /// </summary>
    private bool _flowVisible;

    /// <summary>
    /// "Process" while the flow strip is open - the highlighted card names the
    /// one you are on. Once the strip is closed nothing else on the page says
    /// which process these steps belong to, so the heading does.
    /// </summary>
    private string HeaderTitle =>
        !_flowVisible && SelectedProcess is { } selected ? selected.Name : "Process";

    /// <summary>Where to put the preview. Null falls back to the strip's left.</summary>
    private double? _previewX;
    private double? _previewY;
    private string? _folderName;
    private int _resolvedFolderId;
    private int? _selectedProcessId;
    private int? _selectedStepId;
    private bool _loading = true;

    [Inject]
    private IProcessService Processes { get; set; } = null!;

    [Inject]
    private IProcessStepService StepService { get; set; } = null!;

    [Inject]
    private IProcessFolderService Folders { get; set; } = null!;

    [Inject]
    private IDialogService Dialogs { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Set by /processes/{FolderId}. Null on the root route.</summary>
    [Parameter]
    public int? FolderId { get; set; }

    /// <summary>Set by /processes/{FolderId}/{ProcessId}.</summary>
    [Parameter]
    public int? ProcessId { get; set; }

    private ProcessListItemDto? SelectedProcess =>
        _processes.FirstOrDefault(p => p.Id == _selectedProcessId);

    private ProcessStepDto? SelectedStep =>
        _steps.FirstOrDefault(s => s.Id == _selectedStepId);

    private int ProcessIndex =>
        _processes.ToList().FindIndex(p => p.Id == _selectedProcessId);

    private int StepIndex =>
        _steps.ToList().FindIndex(s => s.Id == _selectedStepId);

    /// <summary>
    /// There is a previous step if this is not the first step of the first
    /// process. Off the front of a process, Back drops into the one before it.
    /// </summary>
    private bool HasPrevious => StepIndex > 0 || ProcessIndex > 0;

    /// <summary>
    /// There is a next step if this is not the last step of the last process.
    /// Off the end of a process, Next moves into the one after it.
    /// </summary>
    private bool HasNext =>
        (StepIndex >= 0 && StepIndex < _steps.Count - 1)
        || (ProcessIndex >= 0 && ProcessIndex < _processes.Count - 1);

    /// <summary>Tells the user where they are, since Next silently crosses processes.</summary>
    private string NavHint
    {
        get
        {
            if (SelectedProcess is null)
            {
                return string.Empty;
            }

            string where = $"Process {ProcessIndex + 1} of {_processes.Count}";

            return _steps.Count == 0
                ? where
                : $"{where}  -  Step {StepIndex + 1} of {_steps.Count}";
        }
    }

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            IReadOnlyList<ProcessFolderDto> folders = await Folders.GetWithCountsAsync();

            // No folder in the route: open the first one that actually has
            // something in it, so the landing page is never blank for no reason.
            ProcessFolderDto? folder = FolderId is int id
                ? folders.FirstOrDefault(f => f.Id == id)
                : folders.FirstOrDefault(f => f.ProcessCount > 0)
                  ?? (folders.Count > 0 ? folders[0] : null);

            if (folder is null)
            {
                _processes = [];
                _steps = [];
                return;
            }

            _resolvedFolderId = folder.Id;
            _folderName = folder.Name;
            _processes = await Processes.GetByFolderAsync(folder.Id);

            int? target = ProcessId is int requested && _processes.Any(p => p.Id == requested)
                ? requested
                : _processes.Count > 0 ? _processes[0].Id : null;

            await SetProcessAsync(target);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SetProcessAsync(int? processId, bool selectLastStep = false)
    {
        _selectedProcessId = processId;

        if (processId is not int id)
        {
            _steps = [];
            _selectedStepId = null;
            return;
        }

        _steps = await StepService.GetByProcessAsync(id);

        _selectedStepId = _steps.Count == 0
            ? null
            : selectLastStep ? _steps[^1].Id : _steps[0].Id;
    }

    private Task SelectProcessAsync(int processId) => SetProcessAsync(processId);

    private void SelectStep(int stepId) => _selectedStepId = stepId;

    private async Task GoPreviousAsync()
    {
        if (StepIndex > 0)
        {
            _selectedStepId = _steps[StepIndex - 1].Id;
            return;
        }

        if (ProcessIndex > 0)
        {
            // Off the front of this process - land on the LAST step of the one
            // before, so Back and Next are exact opposites.
            await SetProcessAsync(_processes[ProcessIndex - 1].Id, selectLastStep: true);
        }
    }

    private async Task GoNextAsync()
    {
        if (StepIndex >= 0 && StepIndex < _steps.Count - 1)
        {
            _selectedStepId = _steps[StepIndex + 1].Id;
            return;
        }

        if (ProcessIndex >= 0 && ProcessIndex < _processes.Count - 1)
        {
            await SetProcessAsync(_processes[ProcessIndex + 1].Id);
        }
    }

    /// <summary>
    /// Asks which, then does it. The question only has two answers and both are
    /// real, so it is a dialog rather than a menu - MudMenu has swallowed clicks
    /// in this codebase before and this must not fail silently.
    /// </summary>
    private async Task AddSomethingAsync()
    {
        AddChoice choice = await AddChooserDialog.ShowAsync(
            Dialogs,
            SelectedProcess?.Name ?? "this process",
            _folderName ?? "this folder");

        switch (choice)
        {
            case AddChoice.Step:
                await AddStepAsync();
                break;

            case AddChoice.Process:
                await AddProcessAsync();
                break;

            default:
                break;
        }
    }

    private async Task AddProcessAsync()
    {
        if (!await ProcessDialog.ShowAsync(Dialogs, _resolvedFolderId))
        {
            return;
        }

        Snackbar.Add("Process added.", Severity.Success);
        await LoadAsync();
    }

    private async Task EditProcessAsync()
    {
        if (_selectedProcessId is not int id)
        {
            return;
        }

        if (!await ProcessDialog.ShowAsync(Dialogs, _resolvedFolderId, id))
        {
            return;
        }

        Snackbar.Add("Process saved.", Severity.Success);

        // A delete leaves nothing selected, so reload the folder from scratch.
        ProcessId = null;
        await LoadAsync();
    }

    private async Task AddStepAsync()
    {
        if (_selectedProcessId is not int id)
        {
            return;
        }

        if (!await AddStepDialog.ShowAsync(Dialogs, id))
        {
            return;
        }

        Snackbar.Add("Step added.", Severity.Success);
        await ReloadStepsKeepingSelectionAsync();
    }

    private async Task EditStepAsync(int stepId)
    {
        if (_selectedProcessId is not int id)
        {
            return;
        }

        if (!await AddStepDialog.ShowAsync(Dialogs, id, stepId))
        {
            return;
        }

        Snackbar.Add("Step saved.", Severity.Success);
        await ReloadStepsKeepingSelectionAsync();
    }

    /// <summary>
    /// Reloads this process's steps but keeps the user where they were. A delete
    /// removes the selected step, so fall back to the first rather than a blank.
    /// </summary>
    private async Task ReloadStepsKeepingSelectionAsync()
    {
        if (_selectedProcessId is not int id)
        {
            return;
        }

        int? previous = _selectedStepId;
        _steps = await StepService.GetByProcessAsync(id);
        _processes = await Processes.GetByFolderAsync(_resolvedFolderId);

        _selectedStepId = _steps.Count == 0
            ? null
            : _steps.Any(s => s.Id == previous) ? previous : _steps[0].Id;
    }

    private ProcessListItemDto? HoveredProcess =>
        _hoveredProcessId is int id ? _processes.FirstOrDefault(p => p.Id == id) : null;

    private Task OpenScreenshotAsync(StepImageDto image) =>
        ScreenshotDialog.ShowAsync(Dialogs, image.Url, image.FileName);

    /// <summary>
    /// The business units at work in this folder, each with its own colour.
    /// Collapsing the strip hides the coloured tags on the cards; the legend is
    /// what is left of that information, so it only shows while the strip is
    /// closed. Distinct by name, in the order the processes run.
    /// </summary>
    private IReadOnlyList<DepartmentLegendItem> DepartmentLegend =>
    [
        .. _processes
            .Where(p => !string.IsNullOrWhiteSpace(p.DepartmentName))
            .GroupBy(p => p.DepartmentName!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DepartmentLegendItem(
                g.Key,
                g.First().DepartmentColour ?? BrandPalette.DefaultDepartmentColour,
                g.Count()))
    ];

    private void ToggleFlow()
    {
        _flowVisible = !_flowVisible;

        // A preview left showing for a strip that is no longer there would hang
        // in mid-air, anchored to nothing.
        _hoveredProcessId = null;
    }

    /// <summary>
    /// Inline custom properties, because the position is data - it changes with
    /// every card - and a class cannot carry a coordinate.
    /// </summary>
    private string PreviewStyle =>
        _previewX is double x && _previewY is double y
            ? $"--preview-x:{x.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}px;" +
              $"--preview-y:{y.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}px;"
            : string.Empty;

    private void ClearPreview()
    {
        // Only when something is actually showing. An unconditional
        // StateHasChanged here re-renders the whole strip on every pointer exit,
        // including the ones that never showed anything.
        if (_hoveredProcessId is not null)
        {
            _hoveredProcessId = null;
        }
    }

    private ProcessPreview? PreviewFor(int processId) =>
        _previews.TryGetValue(processId, out ProcessPreview? preview) ? preview : null;

    /// <summary>
    /// Fetches what sits underneath a card, once. The description is already on
    /// the card's own DTO; the people are not, and gathering them means reading
    /// the steps.
    /// </summary>
    private async Task LoadPreviewAsync(PreviewRequest request)
    {
        int processId = request.ProcessId;

        // A little below the pointer, so the panel sits under the card rather
        // than on top of the thing being pointed at.
        _previewX = request.X;
        _previewY = request.Y is double y ? y + 28 : null;

        // Already showing this one, and already have its detail. Nothing to do -
        // and doing nothing matters, because this runs on every pointer entry.
        if (_hoveredProcessId == processId && _previews.ContainsKey(processId))
        {
            return;
        }

        // Shown immediately, even before the people have arrived - the panel says
        // "Loading" for the part it does not have yet rather than the band
        // appearing a beat after the pointer.
        _hoveredProcessId = processId;

        if (_previews.ContainsKey(processId) || !_previewsLoading.Add(processId))
        {
            return;
        }

        try
        {
            IReadOnlyList<ProcessStepDto> steps = await StepService.GetByProcessAsync(processId);

            // Distinct, because the same person usually owns several steps, and a
            // list repeating "Sarah" four times tells you nothing.
            IReadOnlyList<string> people =
            [
                .. steps
                    .SelectMany(step => step.Owners)
                    .GroupBy(owner => owner.EmployeeId)
                    .Select(group => group.First())
                    .OrderByDescending(owner => owner.IsPrimary)
                    .ThenBy(owner => owner.FullName, StringComparer.OrdinalIgnoreCase)
                    .Select(Describe)
            ];

            _previews[processId] = new ProcessPreview(people, steps.Count);
            StateHasChanged();
        }
        finally
        {
            _previewsLoading.Remove(processId);
        }
    }

    private static string Describe(StepOwnerDto owner) =>
        string.IsNullOrWhiteSpace(owner.JobTitle)
            ? owner.FullName
            : $"{owner.FullName} - {owner.JobTitle}";

    /// <summary>
    /// Double-click. Opens the process read-only, with every step and everyone on
    /// it - the thing the hover preview is a summary of.
    /// </summary>
    private async Task OpenProcessDetailAsync(int processId)
    {
        // Selecting it first means closing the dialog leaves the page showing
        // what was just being read about, rather than whatever was there before.
        await SelectProcessAsync(processId);
        await ProcessDetailDialog.ShowAsync(Dialogs, processId);
    }

    private string StepRowClass(int stepId) =>
        stepId == _selectedStepId
            ? "app-process__row app-process__row--selected"
            : "app-process__row";
}

/// <summary>One business unit in the collapsed strip's legend.</summary>
/// <param name="Name">The business unit's name.</param>
/// <param name="Colour">Its colour, as configured on the Team screen.</param>
/// <param name="Count">How many processes in this folder belong to it.</param>
public sealed record DepartmentLegendItem(string Name, string Colour, int Count);
