using EventTicketing.Data;
using EventTicketing.Entities;

namespace EventTicketing.Services.Audit;

public interface IAuditService
{
    Task LogAsync(long actorUserId, string action, string entityType, long entityId, object? meta = null, CancellationToken ct = default);
}

