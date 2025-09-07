using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.Models;
using EventTicketing.Entities;

namespace EventTicketing.Controllers;

[ApiController]
[Route("organizer")]
[Authorize(Roles = "Organizer")]
public class OrganizerTicketTypesController : ControllerBase
{
    private readonly AppDbContext _db;
    public OrganizerTicketTypesController(AppDbContext db) => _db = db;

    private long CurrentUserId => long.Parse(User.FindFirstValue("sub")!);

    private async Task<long?> GetMyOrganizerIdAsync(CancellationToken ct)
        => await _db.OrganizerProfiles
            .Where(o => o.UserId == CurrentUserId)
            .Select(o => (long?)o.Id)
            .FirstOrDefaultAsync(ct);

    // Create ticket type for MY event
    [HttpPost("events/{eventId:long}/ticket-types")]
    public async Task<IActionResult> CreateTicketType(long eventId, CreateTicketTypeDto dto, CancellationToken ct)
    {
        var organizerId = await GetMyOrganizerIdAsync(ct);
        if (organizerId is null) return Forbid();

        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return NotFound("Event not found.");
        if (ev.OrganizerId != organizerId.Value) return Forbid();

        if (dto.SalesStart >= dto.SalesEnd) return BadRequest("SalesStart must be before SalesEnd.");
        if (dto.TotalQuantity <= 0) return BadRequest("TotalQuantity must be > 0.");

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

    // Update MY ticket type (ownership via event)
    [HttpPut("ticket-types/{ticketTypeId:long}")]
    public async Task<IActionResult> UpdateTicketType(long ticketTypeId, UpdateTicketTypeDto dto, CancellationToken ct)
    {
        var organizerId = await GetMyOrganizerIdAsync(ct);
        if (organizerId is null) return Forbid();

        var tt = await _db.TicketTypes.Include(t => t.Event).FirstOrDefaultAsync(t => t.Id == ticketTypeId, ct);
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
