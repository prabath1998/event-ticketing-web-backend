using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;

namespace EventTicketing.Controllers;

[ApiController]
[Route("admin/audit-logs")]
[Authorize(Roles = "Admin")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminAuditLogsController(AppDbContext db) => _db = db;
    
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.AdminAuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page-1)*pageSize).Take(pageSize)
            .Select(a => new AuditLogListItemDto(a.Id, a.ActorUserId, a.Action, a.EntityType, a.EntityId, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }
}