using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Domain.Entities;
using TeamPortal.Domain.Enums;
using TeamPortal.Infrastructure.Data;

namespace TeamPortal.Infrastructure.Services;

internal sealed class SqlProcessFolderService : SqlServiceBase, IProcessFolderService
{
    private readonly IValidator<CreateProcessFolderRequest> _createValidator;

    public SqlProcessFolderService(
        IDbContextFactory<PortalDbContext> factory,
        ICurrentUserService currentUser,
        IValidator<CreateProcessFolderRequest> createValidator)
        : base(factory, currentUser) => _createValidator = createValidator;

    public async Task<IReadOnlyList<ProcessFolderDto>> GetWithCountsAsync(
        CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await db.ProcessFolders
            .AsNoTracking()
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .Select(f => new ProcessFolderDto(
                f.Id,
                f.Name,
                // Archived processes are hidden from the sidebar badge.
                f.Processes.Count(p => !p.IsArchived)))
            .ToListAsync(ct);
    }

    public async Task<Result<int>> CreateAsync(
        CreateProcessFolderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (AuthorisationGuard.RequireEdit<int>(CurrentUser) is { } denied)
        {
            return denied;
        }

        if (await _createValidator.CheckAsync<CreateProcessFolderRequest, int>(request, ct) is { } invalid)
        {
            return invalid;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        string name = request.Name.Trim();
        string needle = name.ToUpperInvariant();

        if (await db.ProcessFolders.AnyAsync(f => f.Name.ToUpper() == needle, ct))
        {
            return Result<int>.Invalid(
                nameof(request.Name), "A folder with that name already exists.");
        }

        int lastPosition = await db.ProcessFolders.Select(f => (int?)f.SortOrder).MaxAsync(ct) ?? 0;

        var folder = new ProcessFolder { Name = name, SortOrder = lastPosition + 1 };

        db.ProcessFolders.Add(folder);
        await db.SaveChangesAsync(ct);

        WriteActivity(db, ActivityAction.Created, "ProcessFolder", folder.Id, $"Added folder {name}");
        await db.SaveChangesAsync(ct);

        return Result<int>.Success(folder.Id);
    }

    public async Task<FolderContentsDto?> GetContentsSummaryAsync(
        int id, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        ProcessFolder? folder = await db.ProcessFolders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (folder is null)
        {
            return null;
        }

        // Two counts, one round trip each. The step count crosses the join, so
        // it cannot come from the folder row.
        int processes = await db.Processes.CountAsync(p => p.ProcessFolderId == id, ct);

        int steps = await db.ProcessSteps
            .CountAsync(s => s.Process.ProcessFolderId == id, ct);

        return new FolderContentsDto(folder.Id, folder.Name, processes, steps);
    }

    public async Task<Result> DeleteWithEverythingInsideAsync(
        int id, CancellationToken ct = default)
    {
        if (AuthorisationGuard.RequireEdit(CurrentUser) is { } denied)
        {
            return denied;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        ProcessFolder? folder = await db.ProcessFolders
            .Include(f => f.Processes)
            .ThenInclude(p => p.Steps)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (folder is null)
        {
            return Result.NotFound("That folder");
        }

        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

        int processes = folder.Processes.Count;
        int steps = folder.Processes.Sum(p => p.Steps.Count);

        // Removed by hand rather than left to the foreign key, because the
        // database would hard-delete them and everything here is soft-deleted.
        // Each Remove goes through the SaveChanges override.
        foreach (Process process in folder.Processes)
        {
            foreach (ProcessStep step in process.Steps)
            {
                db.ProcessSteps.Remove(step);
            }

            db.Processes.Remove(process);
        }

        db.ProcessFolders.Remove(folder);

        // The activity log survives all of it - history outlives the record.
        WriteActivity(db, ActivityAction.Deleted, "ProcessFolder", id,
            $"Removed folder {folder.Name} with {processes} process(es) and {steps} step(s)");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (AuthorisationGuard.RequireEdit(CurrentUser) is { } denied)
        {
            return denied;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        ProcessFolder? folder = await db.ProcessFolders.FirstOrDefaultAsync(f => f.Id == id, ct);

        if (folder is null)
        {
            return Result.NotFound("That folder");
        }

        if (await db.Processes.AnyAsync(p => p.ProcessFolderId == id, ct))
        {
            return Result.Invalid(
                ValidationError.FormLevel,
                $"{folder.Name} still contains processes. Empty it first.");
        }

        db.ProcessFolders.Remove(folder);

        WriteActivity(db, ActivityAction.Deleted, "ProcessFolder", id, $"Removed folder {folder.Name}");
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}

internal sealed class SqlProcessService : SqlServiceBase, IProcessService
{
    private readonly IValidator<CreateProcessRequest> _createValidator;
    private readonly IValidator<UpdateProcessRequest> _updateValidator;

    public SqlProcessService(
        IDbContextFactory<PortalDbContext> factory,
        ICurrentUserService currentUser,
        IValidator<CreateProcessRequest> createValidator,
        IValidator<UpdateProcessRequest> updateValidator)
        : base(factory, currentUser)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<ProcessListItemDto>> GetByFolderAsync(
        int folderId, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await db.Processes
            .AsNoTracking()
            .Where(p => p.ProcessFolderId == folderId && !p.IsArchived)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .Select(p => new ProcessListItemDto(
                p.Id,
                p.Name,
                p.Description,
                p.SortOrder,
                p.DepartmentId,
                p.Department == null ? null : p.Department.Name,
                p.Department == null ? null : p.Department.ColourHex,
                p.StepStatusId,
                p.StepStatus.Name,
                p.StepStatus.ColourHex,
                p.Steps.Count))
            .ToListAsync(ct);
    }

    public async Task<ProcessDetailDto?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        Process? process = await db.Processes
            .AsNoTracking()
            .Where(p => p.Id == id && !p.IsArchived)
            .FirstOrDefaultAsync(ct);

        if (process is null)
        {
            return null;
        }

        // One query for the steps, not one per step - see the N+1 test in
        // Frontend/BuildPlan.md, phase B2.
        IReadOnlyList<ProcessStepDto> steps = await StepProjection
            .For(db)
            .Where(s => s.ProcessId == id)
            .OrderBy(s => s.StepNumber)
            .Select(StepProjection.ToDto)
            .ToListAsync(ct);

        string? departmentName = process.DepartmentId is null
            ? null
            : await db.Departments
                .Where(d => d.Id == process.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct);

        return new ProcessDetailDto(
            process.Id,
            process.Name,
            process.Description,
            process.ProcessFolderId,
            process.DepartmentId,
            departmentName,
            process.StepStatusId,
            steps);
    }

    public async Task<Result<int>> CreateAsync(
        CreateProcessRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (AuthorisationGuard.RequireEdit<int>(CurrentUser) is { } denied)
        {
            return denied;
        }

        if (await _createValidator.CheckAsync<CreateProcessRequest, int>(request, ct) is { } invalid)
        {
            return invalid;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        if (!await db.ProcessFolders.AnyAsync(f => f.Id == request.ProcessFolderId, ct))
        {
            return Result<int>.Invalid(
                nameof(request.ProcessFolderId), "That folder no longer exists.");
        }

        string name = request.Name.Trim();

        // Uniqueness is per folder, not global. The same process name may
        // legitimately exist in two folders.
        if (await IsDuplicateNameAsync(db, name, request.ProcessFolderId, excludingId: null, ct))
        {
            return Result<int>.Invalid(
                nameof(request.Name), "A process with that name already exists in this folder.");
        }

        int defaultStatusId = await db.StepStatuses
            .Where(s => s.IsDefault)
            .Select(s => s.Id)
            .FirstAsync(ct);

        int lastPosition = await db.Processes
            .Where(p => p.ProcessFolderId == request.ProcessFolderId)
            .Select(p => (int?)p.SortOrder)
            .MaxAsync(ct) ?? 0;

        var process = new Process
        {
            Name = name,
            Description = request.Description?.Trim(),
            ProcessFolderId = request.ProcessFolderId,
            DepartmentId = request.DepartmentId,
            StepStatusId = defaultStatusId,
            // New processes land at the end of their folder.
            SortOrder = lastPosition + 1
        };

        db.Processes.Add(process);
        await db.SaveChangesAsync(ct);

        WriteActivity(db, ActivityAction.Created, "Process", process.Id, $"Added process {name}");
        await db.SaveChangesAsync(ct);

        return Result<int>.Success(process.Id);
    }

    public async Task<Result> UpdateAsync(
        UpdateProcessRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (AuthorisationGuard.RequireEdit(CurrentUser) is { } denied)
        {
            return denied;
        }

        if (await _updateValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        Process? process = await db.Processes.FirstOrDefaultAsync(p => p.Id == request.Id, ct);

        if (process is null)
        {
            return Result.NotFound("That process");
        }

        string name = request.Name.Trim();

        if (await IsDuplicateNameAsync(db, name, process.ProcessFolderId, request.Id, ct))
        {
            return Result.Invalid(
                nameof(request.Name), "A process with that name already exists in this folder.");
        }

        if (request.DepartmentId is int departmentId &&
            !await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
        {
            return Result.Invalid(nameof(request.DepartmentId), "That department no longer exists.");
        }

        if (request.StepStatusId is int statusId &&
            !await db.StepStatuses.AnyAsync(s => s.Id == statusId, ct))
        {
            return Result.Invalid(nameof(request.StepStatusId), "That status does not exist.");
        }

        process.Name = name;
        process.Description = request.Description?.Trim();
        process.DepartmentId = request.DepartmentId;
        process.StepStatusId = request.StepStatusId ?? process.StepStatusId;

        WriteActivity(db, ActivityAction.Updated, "Process", process.Id, $"Updated process {name}");
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (AuthorisationGuard.RequireEdit(CurrentUser) is { } denied)
        {
            return denied;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        Process? process = await db.Processes
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (process is null)
        {
            return Result.NotFound("That process");
        }

        // Cascade by hand rather than leaving it to the foreign key: the database
        // would hard-delete the steps, and these have to be soft-deleted like
        // everything else. Removing them puts them through the SaveChanges override.
        foreach (ProcessStep step in process.Steps)
        {
            db.ProcessSteps.Remove(step);
        }

        db.Processes.Remove(process);

        // The activity log survives - history outlives the record.
        WriteActivity(db, ActivityAction.Deleted, "Process", id, $"Removed process {process.Name}");
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static Task<bool> IsDuplicateNameAsync(
        PortalDbContext db, string name, int folderId, int? excludingId, CancellationToken ct)
    {
        string needle = name.ToUpperInvariant();

        return db.Processes.AnyAsync(
            p => p.Id != excludingId &&
                 p.ProcessFolderId == folderId &&
                 p.Name.ToUpper() == needle,
            ct);
    }
}
