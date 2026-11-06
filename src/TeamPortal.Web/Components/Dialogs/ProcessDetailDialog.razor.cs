using Microsoft.AspNetCore.Components;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Web.Components.Dialogs;

public partial class ProcessDetailDialog : ComponentBase
{
    private ProcessDetailDto? _process;
    private IReadOnlyList<ProcessStepDto> _steps = [];
    private IReadOnlyList<StepOwnerDto> _people = [];
    private string _statusName = string.Empty;
    private bool _loading = true;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public int ProcessId { get; set; }

    [Inject]
    private IProcessService Processes { get; set; } = null!;

    /// <summary>
    /// ProcessDetailDto carries the status ID but not its name - it was shaped
    /// for a screen that already had the lookup loaded. This dialog does not.
    /// </summary>
    [Inject]
    private IStepStatusService Statuses { get; set; } = null!;

    public static async Task ShowAsync(IDialogService dialogs, int processId)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<ProcessDetailDialog>
        {
            { x => x.ProcessId, processId }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            BackdropClick = true
        };

        IDialogReference dialog =
            await dialogs.ShowAsync<ProcessDetailDialog>("Process detail", parameters, options);

        await dialog.Result;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // One call. GetDetailAsync returns the process and every step with its
            // owners already attached, which is exactly this dialog.
            _process = await Processes.GetDetailAsync(ProcessId);
            _steps = _process?.Steps ?? [];

            if (_process is not null)
            {
                IReadOnlyList<StepStatusDto> statuses = await Statuses.GetAllAsync();

                _statusName = statuses
                    .FirstOrDefault(s => s.Id == _process.StepStatusId)?.Name ?? string.Empty;
            }

            // The same person usually owns several steps. Listed once, primary
            // owners first, so this reads as "who is involved" rather than as a
            // tally of assignments.
            _people =
            [
                .. _steps
                    .SelectMany(step => step.Owners)
                    .GroupBy(owner => owner.EmployeeId)
                    .Select(group => group.OrderByDescending(o => o.IsPrimary).First())
                    .OrderByDescending(owner => owner.IsPrimary)
                    .ThenBy(owner => owner.FullName, StringComparer.OrdinalIgnoreCase)
            ];
        }
        finally
        {
            _loading = false;
        }
    }

    private void Close() => MudDialog.Close();
}
