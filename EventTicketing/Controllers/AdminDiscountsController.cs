using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Services.Audit;
using System.Security.Claims;
using EventTicketing.Entities;
using EventTicketing.Enums;
using EventTicketing.Services.Email;

namespace EventTicketing.Controllers;

[ApiController]
[Route("admin/discounts")]
//[Authorize(Roles = "Admin")]
public class AdminDiscountsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IEmailQueue _emailQueue;  

    public AdminDiscountsController(AppDbContext db, IAuditService audit, IEmailQueue emailQueue)
    {
        _db = db; _audit = audit; _emailQueue = emailQueue;
    }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }
    
    [HttpPut("{eventId:long}/discounts/{id:long}")]
    public async Task<IActionResult> Update(long eventId, long id, [FromBody] CreateDiscountDto dto,
        CancellationToken ct)
    {
        var d = await _db.Discounts.FirstOrDefaultAsync(x => x.Id == id && x.EventId == eventId, ct);
        if (d == null) return NotFound();

        d.Type = dto.Type;
        d.Value = dto.Value;
        d.Scope = dto.Scope;
        d.TicketTypeId = dto.TicketTypeId;
        d.StartsAt = dto.StartsAt;
        d.EndsAt = dto.EndsAt;
        d.MaxUses = dto.MaxUses;
        d.MinSubtotalCents = dto.MinSubtotalCents;
        d.IsActive = dto.IsActive;

        await _db.SaveChangesAsync(ct);
        return Ok(d);
    }

    [HttpGet("{eventId:long}/discounts")]
    public async Task<IActionResult> List(
        long eventId,
        [FromQuery] string? q,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        
        IQueryable<Discount> d = _db.Discounts
            .AsNoTracking()
            .Where(x => x.EventId == eventId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            d = d.Where(x => EF.Functions.Like(x.Code, $"%{s}%"));
        }

        if (active.HasValue)
            d = d.Where(x => x.IsActive == active.Value);

        var total = await d.CountAsync(ct);

        var items = await d
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DiscountAdminListItemDto(
                x.Id,
                x.Code,
                x.Type.ToString(),
                x.Value,
                x.Scope.ToString(),
                x.TicketTypeId,
                x.IsActive,
                x.StartsAt,
                x.EndsAt,
                x.UsedCount))
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
    

    public record CreateDiscountDto(
        string Code,
        DiscountType Type,
        int Value,
        DiscountScope Scope,
        long? TicketTypeId,
        DateTime? StartsAt,
        DateTime? EndsAt,
        int? MaxUses,
        int? MinSubtotalCents,
        bool IsActive);

    [HttpPost("{eventId:long}/discounts")]
    public async Task<IActionResult> Create(long eventId, [FromBody] CreateDiscountDto dto, CancellationToken ct)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        if (!await _db.Events.AnyAsync(e => e.Id == eventId, ct)) return NotFound("Event not found.");

        var exists = await _db.Discounts.AnyAsync(d => d.EventId == eventId && d.Code == code, ct);
        if (exists) return BadRequest("Code already exists for this event.");

        if (dto.Scope == DiscountScope.TicketType && dto.TicketTypeId == null)
            return BadRequest("TicketTypeId is required for TicketType scope.");

        if (dto.TicketTypeId.HasValue)
        {
            var belongs = await _db.TicketTypes.AnyAsync(tt => tt.Id == dto.TicketTypeId && tt.EventId == eventId, ct);
            if (!belongs) return BadRequest("ticketTypeId does not belong to this event.");
        }

        var d = new Discount
        {
            EventId = eventId,
            Code = code,
            Type = dto.Type,
            Value = dto.Value,
            Scope = dto.Scope,
            TicketTypeId = dto.TicketTypeId,
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            MaxUses = dto.MaxUses,
            MinSubtotalCents = dto.MinSubtotalCents,
            IsActive = dto.IsActive
        };

        _db.Discounts.Add(d);
        await _db.SaveChangesAsync(ct);
      
        var ev = await _db.Events.AsNoTracking()
            .Where(x => x.Id == eventId)
            .Select(x => new { x.Title, x.StartTime, x.EndTime })
            .SingleAsync(ct);

        string discountText = d.Type == Enums.DiscountType.Percentage
            ? $"{d.Value}% off"
            : $"{(d.Value / 100.0):0.00} {d.TicketType?.Currency ?? "LKR"} off";

        string period = (d.StartsAt, d.EndsAt) switch
        {
            (not null, not null) => $"{d.StartsAt:yyyy-MM-dd} → {d.EndsAt:yyyy-MM-dd}",
            (not null, null)     => $"from {d.StartsAt:yyyy-MM-dd}",
            (null, not null)     => $"until {d.EndsAt:yyyy-MM-dd}",
            _                    => "limited time"
        };

        string subject = $"New discount for {ev.Title}: {discountText}";
        string html = $@"
            <div style='font-family:system-ui,Segoe UI,Roboto,Helvetica,Arial'>
              <h2>{ev.Title}</h2>
              <p>We just launched a new discount: <strong>{discountText}</strong>.</p>
              <p>Promo code: <strong style='font-family:monospace'>{d.Code}</strong></p>
              <p>Valid period: <em>{period}</em></p>
              <p>Event dates: {ev.StartTime:yyyy-MM-dd HH:mm} – {ev.EndTime:yyyy-MM-dd HH:mm}</p>
              <p>Hurry while it lasts!</p>
            </div>";
       
        var recipients = await _db.Users
            .AsNoTracking()
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .Select(u => u.Email!)
            .ToListAsync(ct);

        foreach (var email in recipients)
            _emailQueue.Enqueue(new EmailJob(email, subject, html));

        return Ok(d);
    }

    [HttpDelete("{eventId:long}/discounts/{id:long}")]
    public async Task<IActionResult> Delete(long eventId, long id, CancellationToken ct)
    {
        var d = await _db.Discounts.FirstOrDefaultAsync(x => x.Id == id && x.EventId == eventId, ct);
        if (d == null) return NotFound();
        _db.Discounts.Remove(d);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}