using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Abstractions;
using TeamPortal.Domain.Entities;
using TeamPortal.Domain.Enums;
using TeamPortal.Infrastructure.Data;

namespace TeamPortal.Infrastructure.Services;

internal abstract class SqlServiceBase
{
    private readonly IDbContextFactory<PortalDbContext> _factory;

    protected SqlServiceBase(
        IDbContextFactory<PortalDbContext> factory, ICurrentUserService currentUser)
    {
        _factory = factory;
        CurrentUser = currentUser;
    }

    protected ICurrentUserService CurrentUser { get; }

    /// <summary>
    /// A context for one operation. Always inside `await using` - see the note at
    /// the top of this file for why it is never held any longer than that.
    /// </summary>
    protected Task<PortalDbContext> OpenAsync(CancellationToken ct) =>
        _factory.CreateDbContextAsync(ct);

    /// <summary>
    /// Queues one activity-log row. It is written by the caller's next
    /// SaveChanges, so the log and the change it describes land together or not
    /// at all.
    /// </summary>
    protected void WriteActivity(
        PortalDbContext db, ActivityAction action, string entityName, int? entityId, string summary) =>
        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Summary = summary,
            HappenedAt = DateTime.UtcNow
        });
}
