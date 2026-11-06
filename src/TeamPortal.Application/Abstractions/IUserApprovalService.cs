using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;

namespace TeamPortal.Application.Abstractions;

public interface IUserApprovalService
{
    /// <summary>
    /// Every registered account and where it sits, newest first. Admin only -
    /// every method here checks, because a grid of who has an account is worth
    /// something to an attacker on its own.
    /// </summary>
    Task<IReadOnlyList<UserAccountDto>> GetAllAsync(
        ApprovalFilter filter = ApprovalFilter.All, CancellationToken ct = default);

    /// <summary>How many are waiting. Feeds the badge on the admin nav.</summary>
    Task<int> GetPendingCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Lets them in and emails them to say so. Refused while their email is still
    /// unconfirmed - approving an address nobody has proved they can read would
    /// skip the first gate entirely.
    /// </summary>
    Task<Result> ApproveAsync(int userId, string role, CancellationToken ct = default);

    /// <summary>
    /// Turns them down. No email: telling a stranger their request was seen and
    /// refused is more information than they are owed.
    /// </summary>
    Task<Result> RejectAsync(int userId, CancellationToken ct = default);

    /// <summary>Changes an already-approved account's role.</summary>
    Task<Result> ChangeRoleAsync(int userId, string role, CancellationToken ct = default);
}
