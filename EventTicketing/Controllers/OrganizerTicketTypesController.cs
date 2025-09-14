using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Entities;
using EventTicketing.Models;

namespace EventTicketing.Controllers;

[ApiController]
[Route("organizer")]
[Authorize(Roles = "Organizer")]
public class OrganizerTicketTypesController : ControllerBase
{
    private readonly AppDbContext _db;
    public OrganizerTicketTypesController(AppDbContext db) => _db = db;
  
    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    private Task<long?> GetMyOrganizerIdAsync(long userId, CancellationToken ct) =>
        _db.OrganizerProfiles
           .Where(o => o.UserId == userId)
           .Select(o => (long?)o.Id)
           .FirstOrDefaultAsync(ct);
   
    [HttpPost("events/{eventId:long}/ticket-types")]
    public async Task<IActionResult> CreateTicketType(long eventId, [FromBody] CreateTicketTypeDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null) return Forbid();
       
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return NotFound("Event not found.");
        if (ev.OrganizerId != organizerId.Value) return Forbid();

        if (dto.SalesStart >= dto.SalesEnd) return BadRequest("SalesStart must be before SalesEnd.");
        if (dto.TotalQuantity <= 0) return BadRequest("TotalQuantity must be > 0.");
        if (dto.PerOrderLimit.HasValue && dto.PerOrderLimit <= 0) return BadRequest("PerOrderLimit must be > 0.");

        var tt = new TicketType
        {
            EventId = eventId,
            Name = dto.Name,
            Description = null,
            PriceCents = dto.PriceCents,
            Currency = dto.Currency,
            TotalQuantity = dto.TotalQuantity,
            SalesStart = dto.SalesStart,
            SalesEnd = dto.SalesEnd,
            PerOrderLimit = dto.PerOrderLimit
        };

        _db.TicketTypes.Add(tt);
        await _db.SaveChangesAsync(ct);
        return Created($"/organizer/ticket-types/{tt.Id}", new { tt.Id });
    }
   
    [HttpPut("ticket-types/{ticketTypeId:long}")]
    public async Task<IActionResult> UpdateTicketType(long ticketTypeId, [FromBody] UpdateTicketTypeDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null) return Forbid();

        var tt = await _db.TicketTypes.Include(t => t.Event)
                                      .FirstOrDefaultAsync(t => t.Id == ticketTypeId, ct);
        if (tt is null) return NotFound("Ticket type not found.");
        if (tt.Event.OrganizerId != organizerId.Value) return Forbid();

        if (dto.SalesStart >= dto.SalesEnd) return BadRequest("SalesStart must be before SalesEnd.");
        if (dto.TotalQuantity < tt.SoldQuantity)
            return BadRequest($"TotalQuantity cannot be less than already sold ({tt.SoldQuantity}).");

        tt.Name = dto.Name;
        tt.PriceCents = dto.PriceCents;
        tt.Currency = dto.Currency;
        tt.TotalQuantity = dto.TotalQuantity;
        tt.SalesStart = dto.SalesStart;
        tt.SalesEnd = dto.SalesEnd;
        tt.PerOrderLimit = dto.PerOrderLimit;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
