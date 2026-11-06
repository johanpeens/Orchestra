using FluentValidation;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;

namespace TeamPortal.Mock.Services;

internal sealed class MockEmployeeService : MockServiceBase, IEmployeeService
{
    private readonly IValidator<CreateEmployeeRequest> _createValidator;
    private readonly IValidator<UpdateEmployeeRequest> _updateValidator;

    public MockEmployeeService(
        MockDataStore store,
        MockOptions options,
        IValidator<CreateEmployeeRequest> createValidator,
        IValidator<UpdateEmployeeRequest> updateValidator)
        : base(store, options)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<EmployeeCardDto>> GetByDepartmentAsync(
        int departmentId, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return Cards(Store.LiveEmployees.Where(e => e.IsActive && e.DepartmentId == departmentId));
        }
    }

    public async Task<IReadOnlyList<EmployeeCardDto>> GetAllAsync(CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return Cards(Store.LiveEmployees.Where(e => e.IsActive));
        }
    }

    public async Task<IReadOnlyList<EmployeeCardDto>> SearchAsync(
        string term, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            IEnumerable<MockEmployee> live = Store.LiveEmployees.Where(e => e.IsActive);

            if (string.IsNullOrWhiteSpace(term))
            {
                return Cards(live);
            }

            string needle = term.Trim();

            return Cards(live.Where(e =>
                Contains(e.FullName, needle) ||
                Contains(e.JobTitle, needle) ||
                Contains(DepartmentName(e.DepartmentId), needle)));
        }
    }

    public async Task<IReadOnlyList<EmployeeOptionDto>> GetOptionsAsync(CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            return
            [
                .. Store.LiveEmployees
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                    .Select(e => new EmployeeOptionDto(e.Id, e.FullName, e.JobTitle, e.PhotoUrl))
            ];
        }
    }

    public async Task<EmployeeDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await ReadDelayAsync(ct);

        lock (Store.Gate)
        {
            MockEmployee? e = Store.LiveEmployees.FirstOrDefault(x => x.Id == id);

            return e is null
                ? null
                : new EmployeeDetailDto(
                    e.Id, e.FullName, e.JobTitle, e.Email, e.PhotoUrl,
                    e.StaffType, e.DepartmentId, e.IsActive);
        }
    }

    public async Task<Result<int>> CreateAsync(
        CreateEmployeeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await WriteDelayAsync(ct);

        if (await ValidateAsync<CreateEmployeeRequest, int>(_createValidator, request, ct) is { } invalid)
        {
            return invalid;
        }

        if (SimulatedFailure<int>() is { } failed)
        {
            return failed;
        }

        lock (Store.Gate)
        {
            if (!Store.LiveDepartments.Any(d => d.Id == request.DepartmentId))
            {
                return Result<int>.Invalid(
                    nameof(request.DepartmentId), "That department no longer exists.");
            }

            if (DuplicateEmail(request.Email, excludingId: null))
            {
                return Result<int>.Invalid(
                    nameof(request.Email), "Someone already has that email address.");
            }

            int id = Store.NextEmployeeId();

            Store.Employees.Add(new MockEmployee
            {
                Id = id,
                FullName = request.FullName.Trim(),
                JobTitle = request.JobTitle?.Trim(),
                Email = request.Email?.Trim(),
                StaffType = request.StaffType,
                DepartmentId = request.DepartmentId,
                IsActive = true
            });

            Store.WriteActivity(
                ActivityAction.Created, "Employee", id, $"Added {request.FullName.Trim()}");

            return Result<int>.Success(id);
        }
    }

    public async Task<Result> UpdateAsync(
        UpdateEmployeeRequest request, CancellationToken ct = default)
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
            MockEmployee? employee = Store.LiveEmployees.FirstOrDefault(e => e.Id == request.Id);

            if (employee is null)
            {
                return Result.NotFound("That employee");
            }

            if (!Store.LiveDepartments.Any(d => d.Id == request.DepartmentId))
            {
                return Result.Invalid(
                    nameof(request.DepartmentId), "That department no longer exists.");
            }

            if (DuplicateEmail(request.Email, excludingId: request.Id))
            {
                return Result.Invalid(
                    nameof(request.Email), "Someone already has that email address.");
            }

            employee.FullName = request.FullName.Trim();
            employee.JobTitle = request.JobTitle?.Trim();
            employee.Email = request.Email?.Trim();
            employee.StaffType = request.StaffType;
            employee.DepartmentId = request.DepartmentId;
            employee.IsActive = request.IsActive;
            employee.UpdatedAt = DateTime.UtcNow;

            Store.WriteActivity(
                ActivityAction.Updated, "Employee", employee.Id, $"Updated {employee.FullName}");

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
            MockEmployee? employee = Store.LiveEmployees.FirstOrDefault(e => e.Id == id);

            if (employee is null)
            {
                return Result.NotFound("That employee");
            }

            // Restrict, matching the delete behaviour in Frontend/DataModel.md.
            // Part B's foreign key will refuse this too; rejecting it here means
            // the UI already handles the message.
            if (Store.StepOwners.Any(o => o.EmployeeId == id))
            {
                return Result.Invalid(
                    ValidationError.FormLevel,
                    $"{employee.FullName} is still responsible for one or more process steps. " +
                    "Remove them from those steps first.");
            }

            employee.IsDeleted = true;
            employee.UpdatedAt = DateTime.UtcNow;

            Store.WriteActivity(
                ActivityAction.Deleted, "Employee", id, $"Removed {employee.FullName}");

            return Result.Success();
        }
    }

    // --- Helpers -------------------------------------------------------------

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private string DepartmentName(int departmentId) =>
        Store.Departments.FirstOrDefault(d => d.Id == departmentId)?.Name ?? string.Empty;

    private bool DuplicateEmail(string? email, int? excludingId) =>
        !string.IsNullOrWhiteSpace(email) &&
        Store.LiveEmployees.Any(e =>
            e.Id != excludingId &&
            e.Email is not null &&
            NameMatches(e.Email, email));

    /// <summary>
    /// Projects to fresh DTOs. Never hands back a reference into the store - a
    /// bound field must not be able to edit "the database". Ordered by name to
    /// match what the EF Core implementation will do.
    /// </summary>
    private IReadOnlyList<EmployeeCardDto> Cards(IEnumerable<MockEmployee> source) =>
    [
        .. source
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(e =>
            {
                MockDepartment? d = Store.Departments.FirstOrDefault(x => x.Id == e.DepartmentId);

                return new EmployeeCardDto(
                    e.Id,
                    e.FullName,
                    e.JobTitle,
                    d?.Name ?? string.Empty,
                    d?.ColourHex ?? BrandPalette.DefaultDepartmentColour,
                    e.PhotoUrl,
                    e.StaffType);
            })
    ];
}
