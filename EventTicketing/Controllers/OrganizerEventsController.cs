using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.Models;
using EventTicketing.Entities;
using EventTicketing.Enums;

namespace EventTicketing.Controllers;

[ApiController]
[Route("organizer/events")]
[Authorize(Roles = "Organizer")]
public class OrganizerEventsController : ControllerBase
{
    private readonly AppDbContext _db;
    public OrganizerEventsController(AppDbContext db) => _db = db;
    
    private bool TryGetUserId(out long userId)
    {
        userId = 0;
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    private Task<long?> GetMyOrganizerIdAsync(long userId, CancellationToken ct) =>
        _db.OrganizerProfiles
           .Where(o => o.UserId == userId)
           .Select(o => (long?)o.Id)
           .FirstOrDefaultAsync(ct);
  

    [HttpGet]
    public async Task<IActionResult> ListMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null) return Forbid();

        var q = _db.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == organizerId.Value)
            .OrderByDescending(e => e.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new {
                e.Id, e.Title, e.Status, e.StartTime, e.EndTime,
                e.VenueName, e.LocationCity, e.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateEventDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (dto.StartTime >= dto.EndTime) return BadRequest("StartTime must be before EndTime.");

        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null)
        {
            var profile = new OrganizerProfile
            {
                UserId = userId,
                CompanyName = "Organizer " + userId    
            };
            _db.OrganizerProfiles.Add(profile);
            await _db.SaveChangesAsync(ct);
            organizerId = profile.Id;
        }

        var ev = new Event
        {
            OrganizerId = organizerId.Value,
            Title = dto.Title,
            Description = dto.Description,
            VenueName = dto.VenueName,
            LocationCity = dto.LocationCity,
            LocationAddress = dto.LocationAddress,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Status = EventStatus.Draft
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync(ct);

        if (dto.CategoryIds is { Length: > 0 })
        {
            var catIds = await _db.Categories
                .Where(c => dto.CategoryIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(ct);

            foreach (var cid in catIds)
                _db.EventCategories.Add(new EventCategory { EventId = ev.Id, CategoryId = cid });

            await _db.SaveChangesAsync(ct);
        }

        return CreatedAtAction(nameof(Get), new { id = ev.Id }, new { ev.Id });
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = "IsEventOwner")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new {
                e.Id, e.Title, e.Description, e.Status, e.StartTime, e.EndTime,
                e.VenueName, e.LocationCity, e.LocationAddress,
                Categories = e.EventCategories.Select(ec => ec.CategoryId).ToArray()
            })
            .FirstOrDefaultAsync(ct);

        return ev is null ? NotFound() : Ok(ev);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "IsEventOwner")]
    public async Task<IActionResult> Update(long id, UpdateEventDto dto, CancellationToken ct)
    {
        if (dto.StartTime >= dto.EndTime) return BadRequest("StartTime must be before EndTime.");

        var ev = await _db.Events
            .Include(e => e.EventCategories)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (ev is null) return NotFound();
      
        ev.Title = dto.Title;
        ev.Description = dto.Description;
        ev.VenueName = dto.VenueName;
        ev.LocationCity = dto.LocationCity;
        ev.LocationAddress = dto.LocationAddress;
        ev.StartTime = dto.StartTime;
        ev.EndTime = dto.EndTime;
       
        _db.EventCategories.RemoveRange(ev.EventCategories);
        if (dto.CategoryIds is { Length: > 0 })
        {
            var catIds = await _db.Categories
                .Where(c => dto.CategoryIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(ct);

            foreach (var cid in catIds)
                _db.EventCategories.Add(new EventCategory { EventId = ev.Id, CategoryId = cid });
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:long}/publish")]
    [Authorize(Policy = "IsEventOwner")]
    public async Task<IActionResult> Publish(long id, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (ev is null) return NotFound();

        if (ev.Status == EventStatus.Canceled) return BadRequest("Canceled events cannot be published.");
        if (ev.StartTime <= DateTime.UtcNow) return BadRequest("Event start time must be in the future.");

        ev.Status = EventStatus.Published;
        await _db.SaveChangesAsync(ct);
        return Ok(new { ev.Id, ev.Status });
    }

    [HttpPatch("{id:long}/cancel")]
    [Authorize(Policy = "IsEventOwner")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (ev is null) return NotFound();

        ev.Status = EventStatus.Canceled;
        await _db.SaveChangesAsync(ct);
        return Ok(new { ev.Id, ev.Status });
    }
}
