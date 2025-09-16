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
    private readonly ILogger<OrganizerEventsController> _logger;

    public OrganizerEventsController(AppDbContext db, ILogger<OrganizerEventsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ---------- Helpers ----------
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

    private async Task ProcessImageUpload(IFormFile? imageFile, Event eventEntity)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            // Clear existing image if no new image provided
            eventEntity.ImageData = null;
            eventEntity.ImageContentType = null;
            eventEntity.ImageFileName = null;
            eventEntity.ImageFileSize = null;
            return;
        }

        // Validate image file
        if (imageFile.Length > 5 * 1024 * 1024) // 5MB limit
            throw new ArgumentException("Image file size must be less than 5MB");

        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedContentTypes.Contains(imageFile.ContentType))
            throw new ArgumentException("Invalid image format. Allowed: JPEG, PNG, GIF, WEBP");

        using var memoryStream = new MemoryStream();
        await imageFile.CopyToAsync(memoryStream);

        eventEntity.ImageData = memoryStream.ToArray();
        eventEntity.ImageContentType = imageFile.ContentType;
        eventEntity.ImageFileName = imageFile.FileName;
        eventEntity.ImageFileSize = imageFile.Length;
    }

    // ---------- Endpoints ----------

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
                e.Id,
                e.Title,
                e.Status,
                e.StartTime,
                e.EndTime,
                e.VenueName,
                e.LocationCity,
                e.CreatedAt,
                ImageUrl = e.ImageData != null ? $"/api/events/{e.Id}/image" : null
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateEventDto dto, CancellationToken ct)
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

        try
        {
            await ProcessImageUpload(dto.ImageFile, ev);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

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
                e.Id,
                e.Title,
                e.Description,
                e.Status,
                e.StartTime,
                e.EndTime,
                e.VenueName,
                e.LocationCity,
                e.LocationAddress,
                ImageUrl = e.ImageData != null ? $"/api/events/{e.Id}/image" : null,
                Categories = e.EventCategories.Select(ec => ec.CategoryId).ToArray()
            })
            .FirstOrDefaultAsync(ct);

        return ev is null ? NotFound() : Ok(ev);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "IsEventOwner")]
    public async Task<IActionResult> Update(long id, [FromForm] UpdateEventDto dto, CancellationToken ct)
    {
        if (dto.StartTime >= dto.EndTime) return BadRequest("StartTime must be before EndTime.");

        var ev = await _db.Events
            .Include(e => e.EventCategories)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (ev is null) return NotFound();

        // Update fields
        ev.Title = dto.Title;
        ev.Description = dto.Description;
        ev.VenueName = dto.VenueName;
        ev.LocationCity = dto.LocationCity;
        ev.LocationAddress = dto.LocationAddress;
        ev.StartTime = dto.StartTime;
        ev.EndTime = dto.EndTime;

        try
        {
            await ProcessImageUpload(dto.ImageFile, ev);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        // Replace categories
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

    [HttpDelete("{id:long}/image")]
    [Authorize(Policy = "IsEventOwner")]
    public async Task<IActionResult> DeleteImage(long id, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (ev is null) return NotFound();

        ev.ImageData = null;
        ev.ImageContentType = null;
        ev.ImageFileName = null;
        ev.ImageFileSize = null;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Keep your existing Publish and Cancel methods...
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