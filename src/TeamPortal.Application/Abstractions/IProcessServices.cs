using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;

namespace TeamPortal.Application.Abstractions;

public interface IProcessFolderService
{
    /// <summary>
    /// The sidebar folders on screen 02, each with its process count.
    /// Counts only non-archived, non-deleted processes.
    /// </summary>
    Task<IReadOnlyList<ProcessFolderDto>> GetWithCountsAsync(CancellationToken ct = default);

    Task<Result<int>> CreateAsync(CreateProcessFolderRequest request, CancellationToken ct = default);

    /// <summary>Refused while the folder still holds processes.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Deletes the folder and everything inside it - every process, and every
    /// step of every process.
    ///
    /// A separate method rather than a flag on DeleteAsync, and named at length
    /// on purpose. The safe delete above is what a mis-click reaches; this one
    /// has to be asked for. Soft delete throughout, so the rows survive in the
    /// database and a mistake is recoverable by someone with access to it.
    /// </summary>
    Task<Result> DeleteWithEverythingInsideAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// What deleting a folder would take with it. Feeds the confirmation, so it
    /// can say "7 processes and 12 steps" rather than "are you sure?".
    /// </summary>
    Task<FolderContentsDto?> GetContentsSummaryAsync(int id, CancellationToken ct = default);
}

public interface IProcessService
{
    /// <summary>Processes inside one folder, in SortOrder then name. Excludes archived.</summary>
    Task<IReadOnlyList<ProcessListItemDto>> GetByFolderAsync(int folderId, CancellationToken ct = default);

    /// <summary>
    /// One process and every one of its steps, ready to render screen 02 in a single call.
    /// Steps arrive in StepNumber order. Returns null if the process does not exist,
    /// is archived, or is soft-deleted.
    /// </summary>
    Task<ProcessDetailDto?> GetDetailAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(CreateProcessRequest request, CancellationToken ct = default);

    Task<Result> UpdateAsync(UpdateProcessRequest request, CancellationToken ct = default);

    /// <summary>Soft-deletes the process and every step under it.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IProcessStepService
{
    Task<ProcessStepDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Steps for one process, always in StepNumber order.</summary>
    Task<IReadOnlyList<ProcessStepDto>> GetByProcessAsync(int processId, CancellationToken ct = default);

    /// <summary>
    /// Creates the step, its owners and its screenshot as one unit. Either all of it
    /// lands or none of it does - a half-saved step must never be left behind.
    /// The new step is appended with the next free StepNumber.
    /// </summary>
    Task<Result<int>> CreateAsync(CreateStepRequest request, CancellationToken ct = default);

    Task<Result> UpdateAsync(UpdateStepRequest request, CancellationToken ct = default);

    /// <summary>Soft-deletes the step, then renumbers the survivors so there are no gaps.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Drag-to-reorder. Shifts the steps in between and renumbers.</summary>
    Task<Result> MoveAsync(MoveStepRequest request, CancellationToken ct = default);

    /// <summary>
    /// Renumbers every step in a process to 1..n with no gaps and no duplicates.
    /// Idempotent - running it on already-correct steps changes nothing.
    /// </summary>
    Task<Result> ResequenceAsync(int processId, CancellationToken ct = default);
}

public interface IStepStatusService
{
    /// <summary>The status lookup, in SortOrder. Cached - these change rarely.</summary>
    Task<IReadOnlyList<StepStatusDto>> GetAllAsync(CancellationToken ct = default);
}
