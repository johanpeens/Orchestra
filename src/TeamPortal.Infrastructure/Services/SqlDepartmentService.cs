using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Domain.Entities;
using TeamPortal.Domain.Enums;
using TeamPortal.Infrastructure.Data;

namespace TeamPortal.Infrastructure.Services;

internal sealed class SqlDepartmentService : SqlServiceBase, IDepartmentService
{
    private readonly IValidator<CreateDepartmentRequest> _createValidator;
    private readonly IValidator<UpdateDepartmentRequest> _updateValidator;

    public SqlDepartmentService(
        IDbContextFactory<PortalDbContext> factory,
        ICurrentUserService currentUser,
        IValidator<CreateDepartmentRequest> createValidator,
        IValidator<UpdateDepartmentRequest> updateValidator)
        : base(factory, currentUser)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<DepartmentListItemDto>> GetWithCountsAsync(
        CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        // One query with a correlated count, not one query per department. The
        // count is calculated on every read and never stored, so it cannot drift.
        return await db.Departments
            .AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .Select(d => new DepartmentListItemDto(
                d.Id,
                d.Name,
                d.ColourHex,
                d.Employees.Count(e => e.IsActive && !e.IsDeleted)))
            .ToListAsync(ct);
    }

    public async Task<DepartmentDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await db.Departments
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DepartmentDetailDto(d.Id, d.Name, d.ColourHex))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Result<int>> CreateAsync(
        CreateDepartmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (AuthorisationGuard.RequireEdit<int>(CurrentUser) is { } denied)
        {
            return denied;
        }

        if (await _createValidator.CheckAsync<CreateDepartmentRequest, int>(request, ct) is { } invalid)
        {
            return invalid;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        string name = request.Name.Trim();

        if (await IsDuplicateNameAsync(db, name, excludingId: null, ct))
        {
            return Result<int>.Invalid(
                nameof(request.Name), "A department with that name already exists.");
        }

        int lastPosition = await db.Departments
            .Select(d => (int?)d.SortOrder)
            .MaxAsync(ct) ?? 0;

        var department = new Department
        {
            Name = name,
            ColourHex = request.ColourHex,
            SortOrder = lastPosition + 1
        };

        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);

        // Logged after the insert, because the row has no id until then.
        WriteActivity(db, ActivityAction.Created, "Department", department.Id,
            $"Added department {name}");

        await db.SaveChangesAsync(ct);

        return Result<int>.Success(department.Id);
    }

    public async Task<Result> UpdateAsync(
        UpdateDepartmentRequest request, CancellationToken ct = default)
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

        Department? department = await db.Departments
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);

        if (department is null)
        {
            return Result.NotFound("That department");
        }

        string name = request.Name.Trim();

        // Excluding its own id is the whole trick. Without it, saving a department
        // you did not rename fails with "name already taken".
        if (await IsDuplicateNameAsync(db, name, excludingId: request.Id, ct))
        {
            return Result.Invalid(
                nameof(request.Name), "A department with that name already exists.");
        }

        department.Name = name;
        department.ColourHex = request.ColourHex;

        WriteActivity(db, ActivityAction.Updated, "Department", department.Id,
            $"Updated department {name}");

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

        Department? department = await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);

        if (department is null)
        {
            return Result.NotFound("That department");
        }

        // Restrict, checked here rather than left to the foreign key, so the user
        // gets a sentence instead of a database error. See Frontend/DataModel.md.
        int staff = await db.Employees.CountAsync(e => e.DepartmentId == id, ct);

        if (staff > 0)
        {
            return Result.Invalid(
                ValidationError.FormLevel,
                $"{department.Name} still has {staff} " +
                $"{(staff == 1 ? "person" : "people")} in it. Move them first.");
        }

        if (await db.ProcessSteps.AnyAsync(s => s.DepartmentId == id, ct))
        {
            return Result.Invalid(
                ValidationError.FormLevel,
                $"{department.Name} is still tagged on one or more process steps.");
        }

        // Remove, not a flag - the SaveChanges override turns this into a soft
        // delete. Doing it by hand here would bypass the audit stamp.
        db.Departments.Remove(department);

        WriteActivity(db, ActivityAction.Deleted, "Department", id,
            $"Removed department {department.Name}");

        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// Case-insensitive by design. SQL Server's default collation ignores case, so
    /// a check that did not would let "sALES" through in the application and then
    /// hit a unique-index violation at the database. UPPER() translates on both
    /// providers, which keeps the two halves of the seam behaving the same.
    /// </summary>
    private static Task<bool> IsDuplicateNameAsync(
        PortalDbContext db, string name, int? excludingId, CancellationToken ct)
    {
        string needle = name.ToUpperInvariant();

        return db.Departments
            .AnyAsync(d => d.Id != excludingId && d.Name.ToUpper() == needle, ct);
    }
}
