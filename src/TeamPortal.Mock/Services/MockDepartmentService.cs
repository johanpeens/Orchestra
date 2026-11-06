using FluentValidation;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;

namespace TeamPortal.Mock.Services;

internal sealed class MockDepartmentService : MockServiceBase, IDepartmentService
{
    private readonly IValidator<CreateDepartmentRequest> _createValidator;
    private readonly IValidator<UpdateDepartmentRequest> _updateValidator;

    public MockDepartmentService(
        MockDataStore store,
        MockOptions options,
        IValidator<CreateDepartmentRequest> createValidator,
        IValidator<UpdateDepartmentRequest> updateValidator)
        : base(store, options)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<DepartmentListItemDto>> GetWithCountsAsync(
        CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return
            [
                .. Store.LiveDepartments
                    .OrderBy(d => d.SortOrder)
                    .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(d => new DepartmentListItemDto(
                        d.Id,
                        d.Name,
                        d.ColourHex,
                        // Calculated, never stored. A stored count drifts.
                        Store.LiveEmployees.Count(e => e.IsActive && e.DepartmentId == d.Id)))
            ];
        }
    }

    public async Task<DepartmentDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            MockDepartment? d = Store.LiveDepartments.FirstOrDefault(x => x.Id == id);

            return d is null
                ? null
                : new DepartmentDetailDto(d.Id, d.Name, d.ColourHex);
        }
    }

    public async Task<Result<int>> CreateAsync(
        CreateDepartmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await ValidateAsync<CreateDepartmentRequest, int>(_createValidator, request, ct) is { } invalid)
        {
            return invalid;
        }

        if (SimulatedFailure<int>() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            if (DuplicateName(request.Name, excludingId: null))
            {
                return Result<int>.Invalid(
                    nameof(request.Name), "A department with that name already exists.");
            }

            int id = Store.NextDepartmentId();

            Store.Departments.Add(new MockDepartment
            {
                Id = id,
                Name = request.Name.Trim(),
                ColourHex = request.ColourHex,
                SortOrder = Store.LiveDepartments.Select(d => d.SortOrder).DefaultIfEmpty(0).Max() + 1
            });

            Store.WriteActivity(
                ActivityAction.Created, "Department", id, $"Added department {request.Name.Trim()}");

            return Result<int>.Success(id);
        }
    }

    public async Task<Result> UpdateAsync(
        UpdateDepartmentRequest request, CancellationToken ct = default)
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
            MockDepartment? department = Store.LiveDepartments.FirstOrDefault(d => d.Id == request.Id);

            if (department is null)
            {
                return Result.NotFound("That department");
            }

            // Excluding its own id is the whole trick. Without it, saving a
            // department without renaming it fails with "name already taken".
            if (DuplicateName(request.Name, excludingId: request.Id))
            {
                return Result.Invalid(
                    nameof(request.Name), "A department with that name already exists.");
            }

            department.Name = request.Name.Trim();
            department.ColourHex = request.ColourHex;
            department.UpdatedAt = DateTime.UtcNow;

            Store.WriteActivity(
                ActivityAction.Updated, "Department", department.Id,
                $"Updated department {department.Name}");

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
            MockDepartment? department = Store.LiveDepartments.FirstOrDefault(d => d.Id == id);

            if (department is null)
            {
                return Result.NotFound("That department");
            }

            // Restrict: move the staff out first. Matches Frontend/DataModel.md.
            int staff = Store.LiveEmployees.Count(e => e.DepartmentId == id);

            if (staff > 0)
            {
                return Result.Invalid(
                    ValidationError.FormLevel,
                    $"{department.Name} still has {staff} " +
                    $"{(staff == 1 ? "person" : "people")} in it. Move them first.");
            }

            if (Store.LiveSteps.Any(s => s.DepartmentId == id))
            {
                return Result.Invalid(
                    ValidationError.FormLevel,
                    $"{department.Name} is still tagged on one or more process steps.");
            }

            department.IsDeleted = true;
            department.UpdatedAt = DateTime.UtcNow;

            Store.WriteActivity(
                ActivityAction.Deleted, "Department", id, $"Removed department {department.Name}");

            return Result.Success();
        }
    }

    private bool DuplicateName(string name, int? excludingId) =>
        Store.LiveDepartments.Any(d => d.Id != excludingId && NameMatches(d.Name, name));
}
