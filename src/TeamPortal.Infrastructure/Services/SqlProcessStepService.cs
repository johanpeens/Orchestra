using System.Linq.Expressions;
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

/// <summary>
/// The step read model, in one place. Both IProcessService.GetDetailAsync and
/// IProcessStepService use it, so a step looks the same wherever it is read from
/// and neither call can drift into an N+1.
/// </summary>
internal static class StepProjection
{
    public static IQueryable<ProcessStep> For(PortalDbContext db) =>
        db.ProcessSteps.AsNoTracking();

    public static Expression<Func<ProcessStep, ProcessStepDto>> ToDto { get; } = s =>
        new ProcessStepDto(
            s.Id,
            s.StepNumber,
            s.Name,
            s.Description,
            s.DepartmentId,
            s.Department == null ? null : s.Department.Name,
            s.Department == null ? null : s.Department.ColourHex,
            s.StepStatusId,
            s.StepStatus.Name,
            s.StepStatus.ColourHex,
            s.Owners
                // Primary owner first, then alphabetically.
                .OrderByDescending(o => o.IsPrimary)
                .ThenBy(o => o.Employee.FullName)
                .Select(o => new StepOwnerDto(
                    o.Employee.Id, o.Employee.FullName, o.Employee.JobTitle,
                    o.Employee.PhotoUrl, o.IsPrimary))
                .ToList(),
            s.Images
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.UploadedAt)
                // MediaPaths.Prefix + column, not MediaPaths.Url(...) - this is
                // an expression tree, and a method call would not translate to SQL.
                .Select(i => new StepImageDto(
                    i.Id, i.FileName, MediaPaths.Prefix + i.StoredPath, i.SizeBytes, i.UploadedAt))
                .ToList());
}

internal sealed class SqlProcessStepService : SqlServiceBase, IProcessStepService
{
    private readonly IValidator<CreateStepRequest> _createValidator;
    private readonly IValidator<UpdateStepRequest> _updateValidator;
    private readonly IValidator<MoveStepRequest> _moveValidator;
    private readonly IFileStore _fileStore;

    public SqlProcessStepService(
        IDbContextFactory<PortalDbContext> factory,
        ICurrentUserService currentUser,
        IValidator<CreateStepRequest> createValidator,
        IValidator<UpdateStepRequest> updateValidator,
        IValidator<MoveStepRequest> moveValidator,
        IFileStore fileStore)
        : base(factory, currentUser)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _moveValidator = moveValidator;
        _fileStore = fileStore;
    }

    public async Task<ProcessStepDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await StepProjection.For(db)
            .Where(s => s.Id == id)
            .Select(StepProjection.ToDto)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ProcessStepDto>> GetByProcessAsync(
        int processId, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await StepProjection.For(db)
            .Where(s => s.ProcessId == processId)
            .OrderBy(s => s.StepNumber)
            .Select(StepProjection.ToDto)
            .ToListAsync(ct);
    }

    public async Task<Result<int>> CreateAsync(
        CreateStepRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Upload, not edit: a Member may add a step with a screenshot.
        if (AuthorisationGuard.RequireUpload<int>(CurrentUser) is { } denied)
        {
            return denied;
        }

        if (await _createValidator.CheckAsync<CreateStepRequest, int>(request, ct) is { } invalid)
        {
            return invalid;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        // Everything that can be refused is checked before anything is written.
        if (!await db.Processes.AnyAsync(p => p.Id == request.ProcessId, ct))
        {
            return Result<int>.Invalid(nameof(request.ProcessId), "That process no longer exists.");
        }

        if (request.DepartmentId is int departmentId &&
            !await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
        {
            return Result<int>.Invalid(
                nameof(request.DepartmentId), "That department no longer exists.");
        }

        IReadOnlyList<int> ownerIds = [.. request.Owners.Distinct()];

        if (!await AllOwnersAvailableAsync(db, ownerIds, ct))
        {
            return Result<int>.Invalid(
                nameof(request.OwnerIds), "One of the chosen people is no longer available.");
        }

        // Saved before the transaction opens. A file write is slow and cannot be
        // rolled back by the database, so it must not be holding locks - and if it
        // fails there is nothing written yet to unwind.
        string? storedPath = null;

        if (request.Screenshot is not null)
        {
            Result<string> saved = await _fileStore.SaveAsync(request.Screenshot, ct);

            if (saved.IsFailure)
            {
                return Result<int>.Invalid(saved.Errors);
            }

            storedPath = saved.Value;
        }

        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

        int defaultStatusId = await db.StepStatuses
            .Where(s => s.IsDefault).Select(s => s.Id).FirstAsync(ct);

        int lastNumber = await db.ProcessSteps
            .Where(s => s.ProcessId == request.ProcessId)
            .Select(s => (int?)s.StepNumber)
            .MaxAsync(ct) ?? 0;

        var step = new ProcessStep
        {
            ProcessId = request.ProcessId,
            StepNumber = lastNumber + 1,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            DepartmentId = request.DepartmentId,
            StepStatusId = defaultStatusId
        };

        db.ProcessSteps.Add(step);
        await db.SaveChangesAsync(ct);

        AddOwners(db, step.Id, ownerIds);

        if (storedPath is not null && request.Screenshot is not null)
        {
            db.StepImages.Add(new StepImage
            {
                ProcessStepId = step.Id,
                FileName = request.Screenshot.FileName,
                StoredPath = storedPath,
                ContentType = request.Screenshot.ContentType,
                SizeBytes = request.Screenshot.SizeBytes
            });
        }

        WriteActivity(db, ActivityAction.Created, "ProcessStep", step.Id,
            $"Added step '{step.Name}'");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<int>.Success(step.Id);
    }

    public async Task<Result> UpdateAsync(UpdateStepRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (AuthorisationGuard.RequireUpload(CurrentUser) is { } denied)
        {
            return denied;
        }

        if (await _updateValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        ProcessStep? step = await db.ProcessSteps
            .Include(s => s.Owners)
            .Include(s => s.Images)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);

        if (step is null)
        {
            return Result.NotFound("That step");
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

        IReadOnlyList<int> ownerIds = [.. request.Owners.Distinct()];

        if (!await AllOwnersAvailableAsync(db, ownerIds, ct))
        {
            return Result.Invalid(
                nameof(request.OwnerIds), "One of the chosen people is no longer available.");
        }

        // Saved before anything is written, like Create does: a rejected file
        // should not leave a half-updated step behind.
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

        step.Name = request.Name.Trim();
        step.Description = request.Description?.Trim();
        step.DepartmentId = request.DepartmentId;
        step.StepStatusId = request.StepStatusId ?? step.StepStatusId;

        if (storedPath is not null && request.Screenshot is not null)
        {
            // Replaces rather than appends. The step detail shows one screenshot,
            // so a second row would be invisible and would never be cleaned up.
            db.StepImages.RemoveRange(step.Images);

            db.StepImages.Add(new StepImage
            {
                ProcessStepId = step.Id,
                FileName = request.Screenshot.FileName,
                StoredPath = storedPath,
                ContentType = request.Screenshot.ContentType,
                SizeBytes = request.Screenshot.SizeBytes
            });
        }

        // Null means "leave the owners alone"; an empty list means "clear them".
        if (request.OwnerIds is not null)
        {
            db.StepOwners.RemoveRange(step.Owners);
            await db.SaveChangesAsync(ct);

            AddOwners(db, step.Id, ownerIds);
        }

        WriteActivity(db, ActivityAction.Updated, "ProcessStep", step.Id,
            $"Updated step '{step.Name}'");

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

        ProcessStep? step = await db.ProcessSteps
            .Include(s => s.Owners)
            .Include(s => s.Images)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (step is null)
        {
            return Result.NotFound("That step");
        }

        int processId = step.ProcessId;
        string name = step.Name;

        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

        // The join rows and images cannot outlive the step, and neither carries
        // audit columns - so these really are deleted, not flagged.
        db.StepOwners.RemoveRange(step.Owners);
        db.StepImages.RemoveRange(step.Images);

        // The step itself goes through the SaveChanges override and is flagged.
        db.ProcessSteps.Remove(step);

        WriteActivity(db, ActivityAction.Deleted, "ProcessStep", id, $"Removed step '{name}'");
        await db.SaveChangesAsync(ct);

        // Close the gap the deletion just made. Safe now rather than before,
        // because the deleted row has dropped out of the filtered unique index.
        await ResequenceAsync(db, processId, ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }

    public async Task<Result> MoveAsync(MoveStepRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (AuthorisationGuard.RequireEdit(CurrentUser) is { } denied)
        {
            return denied;
        }

        if (await _moveValidator.CheckAsync(request, ct) is { } invalid)
        {
            return invalid;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        ProcessStep? step = await db.ProcessSteps
            .FirstOrDefaultAsync(s => s.Id == request.StepId, ct);

        if (step is null)
        {
            return Result.NotFound("That step");
        }

        List<ProcessStep> siblings = await db.ProcessSteps
            .Where(s => s.ProcessId == step.ProcessId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(ct);

        int target = Math.Clamp(request.NewStepNumber, 1, siblings.Count);

        siblings.RemoveAll(s => s.Id == step.Id);
        siblings.Insert(target - 1, step);

        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

        await RenumberAsync(db, siblings, ct);

        WriteActivity(db, ActivityAction.Updated, "ProcessStep", step.Id,
            $"Moved step '{step.Name}' to position {target}");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }

    public async Task<Result> ResequenceAsync(int processId, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

        await ResequenceAsync(db, processId, ct);
        await tx.CommitAsync(ct);

        return Result.Success();
    }

    // --- Helpers -------------------------------------------------------------

    private static Task ResequenceAsync(PortalDbContext db, int processId, CancellationToken ct) =>
        RenumberFromDatabaseAsync(db, processId, ct);

    private static async Task RenumberFromDatabaseAsync(
        PortalDbContext db, int processId, CancellationToken ct)
    {
        List<ProcessStep> ordered = await db.ProcessSteps
            .Where(s => s.ProcessId == processId)
            .OrderBy(s => s.StepNumber)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        await RenumberAsync(db, ordered, ct);
    }

    /// <summary>
    /// Writes 1..n over the list in the order given. Idempotent - if the numbers
    /// are already right, nothing is written at all.
    ///
    /// The two passes are not optional. (ProcessId, StepNumber) is unique, and EF
    /// sends one UPDATE per row, so moving step 2 to 1 while another row still
    /// holds 1 violates the index halfway through. Parking every row on a negative
    /// number first means no two rows ever collide.
    /// </summary>
    private static async Task RenumberAsync(
        PortalDbContext db, List<ProcessStep> ordered, CancellationToken ct)
    {
        bool alreadyCorrect = true;

        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].StepNumber != i + 1)
            {
                alreadyCorrect = false;
                break;
            }
        }

        if (alreadyCorrect)
        {
            return;
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].StepNumber = -(i + 1);
        }

        await db.SaveChangesAsync(ct);

        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].StepNumber = i + 1;
        }

        await db.SaveChangesAsync(ct);
    }

    private static void AddOwners(PortalDbContext db, int stepId, IReadOnlyList<int> ownerIds)
    {
        // The first one chosen is the primary - the face shown on the step card.
        for (int i = 0; i < ownerIds.Count; i++)
        {
            db.StepOwners.Add(new StepOwner
            {
                ProcessStepId = stepId,
                EmployeeId = ownerIds[i],
                IsPrimary = i == 0
            });
        }
    }

    private static async Task<bool> AllOwnersAvailableAsync(
        PortalDbContext db, IReadOnlyList<int> ownerIds, CancellationToken ct)
    {
        if (ownerIds.Count == 0)
        {
            return true;
        }

        int available = await db.Employees
            .CountAsync(e => e.IsActive && ownerIds.Contains(e.Id), ct);

        return available == ownerIds.Count;
    }
}
