using EventTicketing.Data;
using EventTicketing.Entities;

namespace EventTicketing.Services.Audit;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(long actorUserId, string action, string entityType, long entityId, object? meta = null, CancellationToken ct = default)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Meta = meta is null ? null : System.Text.Json.JsonSerializer.Serialize(meta)
        });
        await _db.SaveChangesAsync(ct);
    }
}