using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;

namespace TeamPortal.Application.Abstractions;

public interface IDepartmentService
{
    /// <summary>
    /// The department filter column on screen 05, each with its live employee count.
    /// Counts only active, non-deleted employees.
    /// </summary>
    Task<IReadOnlyList<DepartmentListItemDto>> GetWithCountsAsync(CancellationToken ct = default);

    Task<DepartmentDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(CreateDepartmentRequest request, CancellationToken ct = default);

    Task<Result> UpdateAsync(UpdateDepartmentRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IEmployeeService
{
    /// <summary>Cards for one department, active only, ordered by name.</summary>
    Task<IReadOnlyList<EmployeeCardDto>> GetByDepartmentAsync(int departmentId, CancellationToken ct = default);

    /// <summary>Every active employee, ordered by name. Backs the "All" filter.</summary>
    Task<IReadOnlyList<EmployeeCardDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// The search box on screen 05. Matches name, job title or department name,
    /// case-insensitively. A blank term returns everyone.
    /// </summary>
    Task<IReadOnlyList<EmployeeCardDto>> SearchAsync(string term, CancellationToken ct = default);

    /// <summary>Active employees reduced to id and name, for the owner picker on screen 04.</summary>
    Task<IReadOnlyList<EmployeeOptionDto>> GetOptionsAsync(CancellationToken ct = default);

    Task<EmployeeDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default);

    Task<Result> UpdateAsync(UpdateEmployeeRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
