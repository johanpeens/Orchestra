using FluentValidation;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;

namespace TeamPortal.Mock.Services;

internal sealed class MockStepStatusService : MockServiceBase, IStepStatusService
{
    public MockStepStatusService(MockDataStore store, MockOptions options)
        : base(store, options) { }

    public async Task<IReadOnlyList<StepStatusDto>> GetAllAsync(CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return
            [
                .. Store.StepStatuses
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new StepStatusDto(s.Id, s.Name, s.ColourHex, s.IsDefault))
            ];
        }
    }
}

internal sealed class MockProcessFolderService : MockServiceBase, IProcessFolderService
{
    private readonly IValidator<CreateProcessFolderRequest> _createValidator;

    public MockProcessFolderService(
        MockDataStore store,
        MockOptions options,
        IValidator<CreateProcessFolderRequest> createValidator)
        : base(store, options) => _createValidator = createValidator;

    public async Task<IReadOnlyList<ProcessFolderDto>> GetWithCountsAsync(CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return
            [
                .. Store.LiveFolders
                    .OrderBy(f => f.SortOrder)
                    .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => new ProcessFolderDto(
                        f.Id,
                        f.Name,
                        // Archived processes are hidden from the sidebar badge.
                        Store.LiveProcesses.Count(p => p.ProcessFolderId == f.Id && !p.IsArchived)))
            ];
        }
    }

    public async Task<Result<int>> CreateAsync(
        CreateProcessFolderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await ValidateAsync<CreateProcessFolderRequest, int>(_createValidator, request, ct) is { } invalid)
        {
            return invalid;
        }

        if (SimulatedFailure<int>() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            if (Store.LiveFolders.Any(f => NameMatches(f.Name, request.Name)))
            {
                return Result<int>.Invalid(
                    nameof(request.Name), "A folder with that name already exists.");
            }

            int id = Store.NextProcessFolderId();

            Store.ProcessFolders.Add(new MockProcessFolder
            {
                Id = id,
                Name = request.Name.Trim(),
                SortOrder = Store.LiveFolders.Select(f => f.SortOrder).DefaultIfEmpty(0).Max() + 1
            });

            Store.WriteActivity(
                ActivityAction.Created, "ProcessFolder", id, $"Added folder {request.Name.Trim()}");

            return Result<int>.Success(id);
        }
    }

    public async Task<FolderContentsDto?> GetContentsSummaryAsync(
        int id, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            MockProcessFolder? folder = Store.LiveFolders.FirstOrDefault(f => f.Id == id);

            if (folder is null)
            {
                return null;
            }

            IReadOnlyList<int> processIds =
            [
                .. Store.LiveProcesses.Where(p => p.ProcessFolderId == id).Select(p => p.Id)
            ];

            int steps = Store.LiveSteps.Count(s => processIds.Contains(s.ProcessId));

            return new FolderContentsDto(folder.Id, folder.Name, processIds.Count, steps);
        }
    }

    public async Task<Result> DeleteWithEverythingInsideAsync(
        int id, CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        if (SimulatedFailure() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            MockProcessFolder? folder = Store.LiveFolders.FirstOrDefault(f => f.Id == id);

            if (folder is null)
            {
                return Result.NotFound("That folder");
            }

            List<MockProcess> processes =
            [
                .. Store.LiveProcesses.Where(p => p.ProcessFolderId == id)
            ];

            int stepCount = 0;

            foreach (MockProcess process in processes)
            {
                foreach (MockProcessStep step in
                         Store.LiveSteps.Where(s => s.ProcessId == process.Id).ToList())
                {
                    step.IsDeleted = true;
                    step.UpdatedAt = DateTime.UtcNow;

                    // The join rows and images cannot outlive the step.
                    Store.StepOwners.RemoveAll(o => o.ProcessStepId == step.Id);
                    Store.StepImages.RemoveAll(i => i.ProcessStepId == step.Id);

                    stepCount++;
                }

                process.IsDeleted = true;
                process.UpdatedAt = DateTime.UtcNow;
            }

            folder.IsDeleted = true;
            folder.UpdatedAt = DateTime.UtcNow;

            // The activity log survives all of it.
            Store.WriteActivity(
                ActivityAction.Deleted, "ProcessFolder", id,
                $"Removed folder {folder.Name} with {processes.Count} process(es) and {stepCount} step(s)");

            return Result.Success();
        }
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        if (SimulatedFailure() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            MockProcessFolder? folder = Store.LiveFolders.FirstOrDefault(f => f.Id == id);

            if (folder is null)
            {
                return Result.NotFound("That folder");
            }

            if (Store.LiveProcesses.Any(p => p.ProcessFolderId == id))
            {
                return Result.Invalid(
                    ValidationError.FormLevel,
                    $"{folder.Name} still contains processes. Empty it first.");
            }

            folder.IsDeleted = true;
            folder.UpdatedAt = DateTime.UtcNow;

            Store.WriteActivity(ActivityAction.Deleted, "ProcessFolder", id, $"Removed folder {folder.Name}");

            return Result.Success();
        }
    }
}

internal sealed class MockProcessService : MockServiceBase, IProcessService
{
    private readonly IValidator<CreateProcessRequest> _createValidator;
    private readonly IValidator<UpdateProcessRequest> _updateValidator;
    private readonly MockStepProjector _projector;

    public MockProcessService(
        MockDataStore store,
        MockOptions options,
        IValidator<CreateProcessRequest> createValidator,
        IValidator<UpdateProcessRequest> updateValidator)
        : base(store, options)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _projector = new MockStepProjector(store);
    }

    public async Task<IReadOnlyList<ProcessListItemDto>> GetByFolderAsync(
        int folderId, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return
            [
                .. Store.LiveProcesses
                    .Where(p => p.ProcessFolderId == folderId && !p.IsArchived)
                    .OrderBy(p => p.SortOrder)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(ProjectListItem)
            ];
        }
    }

    public async Task<ProcessDetailDto?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            MockProcess? process = Store.LiveProcesses.FirstOrDefault(p => p.Id == id && !p.IsArchived);

            if (process is null)
            {
                return null;
            }

            return new ProcessDetailDto(
                process.Id,
                process.Name,
                process.Description,
                process.ProcessFolderId,
                process.DepartmentId,
                Store.Departments.FirstOrDefault(d => d.Id == process.DepartmentId)?.Name,
                process.StepStatusId,
                _projector.StepsFor(process.Id));
        }
    }

    public async Task<Result<int>> CreateAsync(
        CreateProcessRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await ValidateAsync<CreateProcessRequest, int>(_createValidator, request, ct) is { } invalid)
        {
            return invalid;
        }

        if (SimulatedFailure<int>() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            if (!Store.LiveFolders.Any(f => f.Id == request.ProcessFolderId))
            {
                return Result<int>.Invalid(
                    nameof(request.ProcessFolderId), "That folder no longer exists.");
            }

            // Uniqueness is per folder, not global. The same process name may
            // legitimately exist in two folders.
            if (DuplicateName(request.Name, request.ProcessFolderId, excludingId: null))
            {
                return Result<int>.Invalid(
                    nameof(request.Name), "A process with that name already exists in this folder.");
            }

            int id = Store.NextProcessId();

            Store.Processes.Add(new MockProcess
            {
                Id = id,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                ProcessFolderId = request.ProcessFolderId,
                DepartmentId = request.DepartmentId,
                StepStatusId = Store.StepStatuses.First(st => st.IsDefault).Id,
                // New processes land at the end of their folder.
                SortOrder = Store.LiveProcesses
                    .Where(p => p.ProcessFolderId == request.ProcessFolderId)
                    .Select(p => p.SortOrder)
                    .DefaultIfEmpty(0)
                    .Max() + 1
            });

            Store.WriteActivity(
                ActivityAction.Created, "Process", id, $"Added process {request.Name.Trim()}");

            return Result<int>.Success(id);
        }
    }

    public async Task<Result> UpdateAsync(
        UpdateProcessRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await ValidateAsync(_updateValidator, request, ct) is { } invalid)
        {
            return invalid;
        }

        if (SimulatedFailure() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            MockProcess? process = Store.LiveProcesses.FirstOrDefault(p => p.Id == request.Id);

            if (process is null)
            {
                return Result.NotFound("That process");
            }

            if (DuplicateName(request.Name, process.ProcessFolderId, excludingId: request.Id))
            {
                return Result.Invalid(
                    nameof(request.Name), "A process with that name already exists in this folder.");
            }

            if (request.DepartmentId is int deptId && !Store.LiveDepartments.Any(d => d.Id == deptId))
            {
                return Result.Invalid(nameof(request.DepartmentId), "That department no longer exists.");
            }

            if (request.StepStatusId is int statusId && !Store.StepStatuses.Any(st => st.Id == statusId))
            {
                return Result.Invalid(nameof(request.StepStatusId), "That status does not exist.");
            }

            process.Name = request.Name.Trim();
            process.Description = request.Description?.Trim();
            process.DepartmentId = request.DepartmentId;
            process.StepStatusId = request.StepStatusId ?? process.StepStatusId;
            process.UpdatedAt = DateTime.UtcNow;

            Store.WriteActivity(
                ActivityAction.Updated, "Process", process.Id, $"Updated process {process.Name}");

            return Result.Success();
        }
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        if (SimulatedFailure() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            MockProcess? process = Store.LiveProcesses.FirstOrDefault(p => p.Id == id);

            if (process is null)
            {
                return Result.NotFound("That process");
            }

            // Cascade: a step has no meaning without its process.
            foreach (MockProcessStep step in Store.LiveSteps.Where(s => s.ProcessId == id).ToList())
            {
                step.IsDeleted = true;
                step.UpdatedAt = DateTime.UtcNow;
                Store.StepOwners.RemoveAll(o => o.ProcessStepId == step.Id);
            }

            process.IsDeleted = true;
            process.UpdatedAt = DateTime.UtcNow;

            // The activity log survives - history outlives the record.
            Store.WriteActivity(
                ActivityAction.Deleted, "Process", id, $"Removed process {process.Name}");

            return Result.Success();
        }
    }

    /// <summary>
    /// Projects a process to its flow-strip card, including the department tag
    /// and status dot the card shows.
    /// </summary>
    private ProcessListItemDto ProjectListItem(MockProcess process)
    {
        MockDepartment? department = process.DepartmentId is null
            ? null
            : Store.Departments.FirstOrDefault(d => d.Id == process.DepartmentId);

        MockStepStatus status =
            Store.StepStatuses.FirstOrDefault(s => s.Id == process.StepStatusId)
            ?? Store.StepStatuses.First(s => s.IsDefault);

        return new ProcessListItemDto(
            process.Id,
            process.Name,
            process.Description,
            process.SortOrder,
            process.DepartmentId,
            department?.Name,
            department?.ColourHex,
            status.Id,
            status.Name,
            status.ColourHex,
            Store.LiveSteps.Count(s => s.ProcessId == process.Id));
    }

    private bool DuplicateName(string name, int folderId, int? excludingId) =>
        Store.LiveProcesses.Any(p =>
            p.Id != excludingId &&
            p.ProcessFolderId == folderId &&
            NameMatches(p.Name, name));
}
