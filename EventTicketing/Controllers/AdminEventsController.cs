using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Enums;
using EventTicketing.Services.Audit;
using System.Security.Claims;

namespace EventTicketing.Controllers;

[ApiController]
[Route("admin/events")]
[Authorize(Roles = "Admin")]
public class AdminEventsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public AdminEventsController(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }
    
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? q,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);

        var evs = _db.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EventStatus>(status, true, out var st))
            evs = evs.Where(e => e.Status == st);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            evs = evs.Where(e => EF.Functions.Like(e.Title, $"%{s}%") ||
                                 (e.Description != null && EF.Functions.Like(e.Description, $"%{s}%")));
        }

        if (from is not null) evs = evs.Where(e => e.StartTime >= from.Value);
        if (to   is not null) evs = evs.Where(e => e.StartTime <  to.Value);

        evs = evs.OrderByDescending(e => e.CreatedAt);

        var total = await evs.CountAsync(ct);
        var items = await evs.Skip((page-1)*pageSize).Take(pageSize)
            .Select(e => new EventAdminListItemDto(
                e.Id, e.OrganizerId, e.Title, e.VenueName, e.LocationCity,
                e.StartTime, e.EndTime, e.Status.ToString(), e.CreatedAt))
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }
   
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> ChangeStatus(long id, [FromBody] ChangeEventStatusDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<EventStatus>(dto.Status, true, out var newStatus))
            return BadRequest("Invalid status.");

        if (!TryGetUserId(out var adminId)) return Unauthorized();

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (ev is null) return NotFound();

        ev.Status = newStatus;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(adminId, "EventStatusChanged", "Event", id, new { newStatus }, ct);
        return Ok(new { ev.Id, ev.Status });
    }
    
}
