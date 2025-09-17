using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Services.Audit;
using System.Security.Claims;

namespace EventTicketing.Controllers;

[ApiController]
[Route("admin/discounts")]
[Authorize(Roles = "Admin")]
public class AdminDiscountsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public AdminDiscountsController(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }
    
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);

        var d = _db.Discounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            d = d.Where(x => EF.Functions.Like(x.Code, $"%{s}%"));
        }
        if (active.HasValue)
            d = d.Where(x => x.IsActive == active.Value);

        var total = await d.CountAsync(ct);
        var items = await d.OrderByDescending(x => x.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x => new DiscountAdminListItemDto(
                x.Id, x.Code, x.Type.ToString(), x.Value, x.Scope.ToString(), x.TicketTypeId,
                x.IsActive, x.StartsAt, x.EndsAt, x.UsedCount))
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }
   
    [HttpPatch("{id:long}/active")]
    public async Task<IActionResult> Toggle(long id, [FromBody] ToggleActiveDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        var disc = await _db.Discounts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (disc is null) return NotFound();

        disc.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(adminId, "DiscountToggled", "Discount", id, new { dto.IsActive }, ct);
        return Ok(new { disc.Id, disc.IsActive });
    }
}
