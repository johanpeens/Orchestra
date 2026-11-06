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

internal sealed class SqlEmployeeService : SqlServiceBase, IEmployeeService
{
    private readonly IValidator<CreateEmployeeRequest> _createValidator;
    private readonly IValidator<UpdateEmployeeRequest> _updateValidator;

    public SqlEmployeeService(
        IDbContextFactory<PortalDbContext> factory,
        ICurrentUserService currentUser,
        IValidator<CreateEmployeeRequest> createValidator,
        IValidator<UpdateEmployeeRequest> updateValidator)
        : base(factory, currentUser)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<EmployeeCardDto>> GetByDepartmentAsync(
        int departmentId, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await Cards(db, e => e.IsActive && e.DepartmentId == departmentId).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EmployeeCardDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await Cards(db, e => e.IsActive).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EmployeeCardDto>> SearchAsync(
        string term, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        if (string.IsNullOrWhiteSpace(term))
        {
            return await Cards(db, e => e.IsActive).ToListAsync(ct);
        }

        // UPPER() on both sides rather than a StringComparison overload, which EF
        // cannot translate. Matches the case-insensitive search the mock did.
        string needle = term.Trim().ToUpperInvariant();

        return await Cards(db, e => e.IsActive && (
                e.FullName.ToUpper().Contains(needle) ||
                (e.JobTitle != null && e.JobTitle.ToUpper().Contains(needle)) ||
                e.Department.Name.ToUpper().Contains(needle)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EmployeeOptionDto>> GetOptionsAsync(CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeOptionDto(e.Id, e.FullName, e.JobTitle, e.PhotoUrl))
            .ToListAsync(ct);
    }

    public async Task<EmployeeDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using PortalDbContext db = await OpenAsync(ct);

        return await db.Employees
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EmployeeDetailDto(
                e.Id, e.FullName, e.JobTitle, e.Email, e.PhotoUrl,
                (Application.Enums.StaffType)e.StaffType, e.DepartmentId, e.IsActive))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Result<int>> CreateAsync(
        CreateEmployeeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (AuthorisationGuard.RequireEdit<int>(CurrentUser) is { } denied)
        {
            return denied;
        }

        if (await _createValidator.CheckAsync<CreateEmployeeRequest, int>(request, ct) is { } invalid)
        {
            return invalid;
        }

        await using PortalDbContext db = await OpenAsync(ct);

        if (!await db.Departments.AnyAsync(d => d.Id == request.DepartmentId, ct))
        {
            return Result<int>.Invalid(
                nameof(request.DepartmentId), "That department no longer exists.");
        }

        string? email = request.Email?.Trim();

        if (await IsDuplicateEmailAsync(db, email, excludingId: null, ct))
        {
            return Result<int>.Invalid(
                nameof(request.Email), "Someone already has that email address.");
        }

        var employee = new Employee
        {
            FullName = request.FullName.Trim(),
            JobTitle = request.JobTitle?.Trim(),
            Email = email,
            StaffType = (StaffType)request.StaffType,
            DepartmentId = request.DepartmentId,
            IsActive = true
        };

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        // Logged after the insert, because the row has no id until then.
        WriteActivity(db, ActivityAction.Created, "Employee", employee.Id,
            $"Added {employee.FullName}");

        await db.SaveChangesAsync(ct);

        return Result<int>.Success(employee.Id);
    }

    public async Task<Result> UpdateAsync(
        UpdateEmployeeRequest request, CancellationToken ct = default)
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

        Employee? employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.Id, ct);

        if (employee is null)
        {
            return Result.NotFound("That employee");
        }

        if (!await db.Departments.AnyAsync(d => d.Id == request.DepartmentId, ct))
        {
            return Result.Invalid(
                nameof(request.DepartmentId), "That department no longer exists.");
        }

        string? email = request.Email?.Trim();

        if (await IsDuplicateEmailAsync(db, email, excludingId: request.Id, ct))
        {
            return Result.Invalid(
                nameof(request.Email), "Someone already has that email address.");
        }

        employee.FullName = request.FullName.Trim();
        employee.JobTitle = request.JobTitle?.Trim();
        employee.Email = email;
        employee.StaffType = (StaffType)request.StaffType;
        employee.DepartmentId = request.DepartmentId;
        employee.IsActive = request.IsActive;

        WriteActivity(db, ActivityAction.Updated, "Employee", employee.Id,
            $"Updated {employee.FullName}");

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

        Employee? employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);

        if (employee is null)
        {
            return Result.NotFound("That employee");
        }

        // Restrict. The foreign key would refuse this too, but catching it here
        // means the user reads a sentence instead of a database error.
        if (await db.StepOwners.AnyAsync(o => o.EmployeeId == id, ct))
        {
            return Result.Invalid(
                ValidationError.FormLevel,
                $"{employee.FullName} is still responsible for one or more process steps. " +
                "Remove them from those steps first.");
        }

        // The SaveChanges override turns this into a soft delete.
        db.Employees.Remove(employee);

        WriteActivity(db, ActivityAction.Deleted, "Employee", id, $"Removed {employee.FullName}");

        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// The card projection, shared by the three list reads. Ordered by name so the
    /// Team screen reads the same however it was filtered.
    /// </summary>
    private static IQueryable<EmployeeCardDto> Cards(
        PortalDbContext db, System.Linq.Expressions.Expression<Func<Employee, bool>> predicate) =>
        db.Employees
            .AsNoTracking()
            .Where(predicate)
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeCardDto(
                e.Id,
                e.FullName,
                e.JobTitle,
                e.Department.Name,
                e.Department.ColourHex,
                e.PhotoUrl,
                (Application.Enums.StaffType)e.StaffType));

    /// <summary>
    /// Email is optional, so two people without one never collide - which is also
    /// why the unique index in phase B1 is filtered on NULL.
    /// </summary>
    private static Task<bool> IsDuplicateEmailAsync(
        PortalDbContext db, string? email, int? excludingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(false);
        }

        string needle = email.ToUpperInvariant();

        return db.Employees.AnyAsync(
            e => e.Id != excludingId && e.Email != null && e.Email.ToUpper() == needle, ct);
    }
}
