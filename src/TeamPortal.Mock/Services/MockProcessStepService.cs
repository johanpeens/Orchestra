using FluentValidation;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;

namespace TeamPortal.Mock.Services;

/// <summary>
/// Turns store rows into <see cref="ProcessStepDto"/>. Shared by the process and
/// step services so both produce identical shapes.
/// </summary>
internal sealed class MockStepProjector
{
    private readonly MockDataStore _store;

    public MockStepProjector(MockDataStore store) => _store = store;

    /// <summary>Steps for one process, always in StepNumber order.</summary>
    public IReadOnlyList<ProcessStepDto> StepsFor(int processId) =>
    [
        .. _store.LiveSteps
            .Where(s => s.ProcessId == processId)
            .OrderBy(s => s.StepNumber)
            .Select(Project)
    ];

    public ProcessStepDto Project(MockProcessStep step)
    {
        MockDepartment? department = step.DepartmentId is null
            ? null
            : _store.Departments.FirstOrDefault(d => d.Id == step.DepartmentId);

        MockStepStatus status =
            _store.StepStatuses.FirstOrDefault(s => s.Id == step.StepStatusId)
            ?? _store.StepStatuses.First(s => s.IsDefault);

        IReadOnlyList<StepOwnerDto> owners =
        [
            .. _store.StepOwners
                .Where(o => o.ProcessStepId == step.Id)
                .Join(_store.LiveEmployees, o => o.EmployeeId, e => e.Id, (o, e) => new { o, e })
                // Primary owner first, then alphabetically.
                .OrderByDescending(x => x.o.IsPrimary)
                .ThenBy(x => x.e.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(x => new StepOwnerDto(
                    x.e.Id, x.e.FullName, x.e.JobTitle, x.e.PhotoUrl, x.o.IsPrimary))
        ];

        IReadOnlyList<StepImageDto> images =
        [
            .. _store.StepImages
                .Where(i => i.ProcessStepId == step.Id)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.UploadedAt)
                .Select(i => new StepImageDto(
                    i.Id, i.FileName, MediaPaths.Url(i.StoredPath), i.SizeBytes, i.UploadedAt))
        ];

        return new ProcessStepDto(
            step.Id,
            step.StepNumber,
            step.Name,
            step.Description,
            step.DepartmentId,
            department?.Name,
            department?.ColourHex,
            status.Id,
            status.Name,
            status.ColourHex,
            owners,
            images);
    }
}

internal sealed class MockProcessStepService : MockServiceBase, IProcessStepService
{
    private readonly IValidator<CreateStepRequest> _createValidator;
    private readonly IValidator<UpdateStepRequest> _updateValidator;
    private readonly IValidator<MoveStepRequest> _moveValidator;
    private readonly IFileStore _fileStore;
    private readonly MockStepProjector _projector;

    public MockProcessStepService(
        MockDataStore store,
        MockOptions options,
        IValidator<CreateStepRequest> createValidator,
        IValidator<UpdateStepRequest> updateValidator,
        IValidator<MoveStepRequest> moveValidator,
        IFileStore fileStore)
        : base(store, options)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _moveValidator = moveValidator;
        _fileStore = fileStore;
        _projector = new MockStepProjector(store);
    }

    public async Task<ProcessStepDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            MockProcessStep? step = Store.LiveSteps.FirstOrDefault(s => s.Id == id);
            return step is null ? null : _projector.Project(step);
        }
    }

    public async Task<IReadOnlyList<ProcessStepDto>> GetByProcessAsync(
        int processId, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return _projector.StepsFor(processId);
        }
    }

    public async Task<Result<int>> CreateAsync(
        CreateStepRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await ValidateAsync<CreateStepRequest, int>(_createValidator, request, ct) is { } invalid)
        {
            return invalid;
        }

        if (SimulatedFailure<int>() is { } failed)
        {
            return failed;
        }

        // Everything that can fail is checked BEFORE anything is written, and the
        // file is saved before the step row exists. That is what makes this
        // all-or-nothing without a transaction to lean on.
        lock (Store.Gate)
        {
            if (!Store.LiveProcesses.Any(p => p.Id == request.ProcessId))
            {
                return Result<int>.Invalid(nameof(request.ProcessId), "That process no longer exists.");
            }

            if (request.DepartmentId is int deptId && !Store.LiveDepartments.Any(d => d.Id == deptId))
            {
                return Result<int>.Invalid(
                    nameof(request.DepartmentId), "That department no longer exists.");
            }

            foreach (int ownerId in request.Owners)
            {
                if (!Store.LiveEmployees.Any(e => e.Id == ownerId && e.IsActive))
                {
                    return Result<int>.Invalid(
                        nameof(request.OwnerIds), "One of the chosen people is no longer available.");
                }
            }
        }

        // Outside the lock: saving a file can be slow, and must not block reads.
        string? storedPath = null;

        if (request.Screenshot is not null)
        {
            Result<string> saved = await _fileStore.SaveAsync(request.Screenshot, ct);

            if (saved.IsFailure)
            {
                // Nothing has been written yet, so there is nothing to unwind.
                return Result<int>.Invalid(saved.Errors);
            }

            storedPath = saved.Value;
        }

        lock (Store.Gate)
        {
            int id = Store.NextProcessStepId();

            Store.ProcessSteps.Add(new MockProcessStep
            {
                Id = id,
                ProcessId = request.ProcessId,
                StepNumber = NextStepNumber(request.ProcessId),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                DepartmentId = request.DepartmentId,
                StepStatusId = Store.StepStatuses.First(s => s.IsDefault).Id
            });

            bool first = true;

            foreach (int ownerId in request.Owners.Distinct())
            {
                Store.StepOwners.Add(new MockStepOwner
                {
                    ProcessStepId = id,
                    EmployeeId = ownerId,
                    IsPrimary = first
                });

                first = false;
            }

            if (storedPath is not null && request.Screenshot is not null)
            {
                Store.StepImages.Add(new MockStepImage
                {
                    Id = Store.NextStepImageId(),
                    ProcessStepId = id,
                    FileName = request.Screenshot.FileName,
                    StoredPath = storedPath,
                    ContentType = request.Screenshot.ContentType,
                    SizeBytes = request.Screenshot.SizeBytes
                });
            }

            Store.WriteActivity(
                ActivityAction.Created, "ProcessStep", id, $"Added step '{request.Name.Trim()}'");

            return Result<int>.Success(id);
        }
    }

    public async Task<Result> UpdateAsync(UpdateStepRequest request, CancellationToken ct = default)
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

        // Outside the lock, and before anything is written - saving a file can be
        // slow, and a rejected one must not leave a half-updated step behind.
        string? storedPath = null;

        if (request.Screenshot is not null)
        {
            Result<string> saved = await _fileStore.SaveAsync(request.Screenshot, ct);

            if (saved.IsFailure)
            {
                return Result.Invalid(saved.Errors);
            }

            storedPath = saved.Value;
        }

        lock (Store.Gate)
        {
            MockProcessStep? step = Store.LiveSteps.FirstOrDefault(s => s.Id == request.Id);

            if (step is null)
            {
                return Result.NotFound("That step");
            }

            if (request.DepartmentId is int deptId && !Store.LiveDepartments.Any(d => d.Id == deptId))
            {
                return Result.Invalid(nameof(request.DepartmentId), "That department no longer exists.");
            }

            if (request.StepStatusId is int statusId && !Store.StepStatuses.Any(s => s.Id == statusId))
            {
                return Result.Invalid(nameof(request.StepStatusId), "That status does not exist.");
            }

            foreach (int ownerId in request.Owners)
            {
                if (!Store.LiveEmployees.Any(e => e.Id == ownerId && e.IsActive))
                {
                    return Result.Invalid(
                        nameof(request.OwnerIds), "One of the chosen people is no longer available.");
                }
            }

            step.Name = request.Name.Trim();
            step.Description = request.Description?.Trim();
            step.DepartmentId = request.DepartmentId;
            step.StepStatusId = request.StepStatusId ?? step.StepStatusId;
            step.UpdatedAt = DateTime.UtcNow;

            if (storedPath is not null && request.Screenshot is not null)
            {
                // Replaces rather than appends - the step detail shows one.
                Store.StepImages.RemoveAll(i => i.ProcessStepId == step.Id);

                Store.StepImages.Add(new MockStepImage
                {
                    Id = Store.NextStepImageId(),
                    ProcessStepId = step.Id,
                    FileName = request.Screenshot.FileName,
                    StoredPath = storedPath,
                    ContentType = request.Screenshot.ContentType,
                    SizeBytes = request.Screenshot.SizeBytes
                });
            }

            if (request.OwnerIds is not null)
            {
                Store.StepOwners.RemoveAll(o => o.ProcessStepId == step.Id);

                bool first = true;

                foreach (int ownerId in request.Owners.Distinct())
                {
                    Store.StepOwners.Add(new MockStepOwner
                    {
                        ProcessStepId = step.Id,
                        EmployeeId = ownerId,
                        IsPrimary = first
                    });

                    first = false;
                }
            }

            Store.WriteActivity(
                ActivityAction.Updated, "ProcessStep", step.Id, $"Updated step '{step.Name}'");

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
            MockProcessStep? step = Store.LiveSteps.FirstOrDefault(s => s.Id == id);

            if (step is null)
            {
                return Result.NotFound("That step");
            }

            int processId = step.ProcessId;
            string name = step.Name;

            step.IsDeleted = true;
            step.UpdatedAt = DateTime.UtcNow;

            // Cascade to the join rows and images - they cannot outlive the step.
            Store.StepOwners.RemoveAll(o => o.ProcessStepId == id);
            Store.StepImages.RemoveAll(i => i.ProcessStepId == id);

            // Close the gap the deletion just made.
            Resequence(processId);

            Store.WriteActivity(ActivityAction.Deleted, "ProcessStep", id, $"Removed step '{name}'");

            return Result.Success();
        }
    }

    public async Task<Result> MoveAsync(MoveStepRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await ValidateAsync(_moveValidator, request, ct) is { } invalid)
        {
            return invalid;
        }

        if (SimulatedFailure() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            MockProcessStep? step = Store.LiveSteps.FirstOrDefault(s => s.Id == request.StepId);

            if (step is null)
            {
                return Result.NotFound("That step");
            }

            List<MockProcessStep> siblings =
            [
                .. Store.LiveSteps
                    .Where(s => s.ProcessId == step.ProcessId)
                    .OrderBy(s => s.StepNumber)
            ];

            int target = Math.Clamp(request.NewStepNumber, 1, siblings.Count);

            siblings.Remove(step);
            siblings.Insert(target - 1, step);

            for (int i = 0; i < siblings.Count; i++)
            {
                siblings[i].StepNumber = i + 1;
            }

            Store.WriteActivity(
                ActivityAction.Updated, "ProcessStep", step.Id,
                $"Moved step '{step.Name}' to position {target}");

            return Result.Success();
        }
    }

    public async Task<Result> ResequenceAsync(int processId, CancellationToken ct = default)
    {
        await WriteDelayAsync(ct);

        lock (Store.Gate)
        {
            Resequence(processId);
            return Result.Success();
        }
    }

    // --- Helpers -------------------------------------------------------------

    private int NextStepNumber(int processId) =>
        Store.LiveSteps
            .Where(s => s.ProcessId == processId)
            .Select(s => s.StepNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;

    /// <summary>
    /// Renumbers a process's steps to 1..n with no gaps and no duplicates.
    /// Idempotent - running it on already-correct steps changes nothing.
    /// Caller must hold the lock.
    /// </summary>
    private void Resequence(int processId)
    {
        List<MockProcessStep> ordered =
        [
            .. Store.LiveSteps
                .Where(s => s.ProcessId == processId)
                .OrderBy(s => s.StepNumber)
                .ThenBy(s => s.Id)
        ];

        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].StepNumber != i + 1)
            {
                ordered[i].StepNumber = i + 1;
            }
        }
    }
}
