using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.Models;
using EventTicketing.Enums;

namespace EventTicketing.Controllers;

[ApiController]
[Route("events")]
public class PublicEventsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PublicEventsController(AppDbContext db) => _db = db;

    [HttpGet]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any,
        VaryByQueryKeys = new[] { "q", "categoryId", "city", "from", "to", "includePast", "page", "pageSize" })]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int? categoryId,
        [FromQuery] string? city,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool includePast = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var now = DateTime.UtcNow;

        var query = _db.Events.AsNoTracking()
            .Where(e => e.Status == EventStatus.Published);

        if (!includePast)
            query = query.Where(e => e.StartTime >= now);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = q.Trim();
            query = query.Where(e =>
                EF.Functions.Like(e.Title, $"%{pattern}%") ||
                (e.Description != null && EF.Functions.Like(e.Description, $"%{pattern}%")));
        }

        if (categoryId is not null)
            query = query.Where(e => e.EventCategories.Any(ec => ec.CategoryId == categoryId.Value));

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(e => e.LocationCity == city);

        if (from is not null) query = query.Where(e => e.StartTime >= from.Value);
        if (to is not null) query = query.Where(e => e.StartTime < to.Value);

        query = query.OrderBy(e => e.StartTime).ThenBy(e => e.Id);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventListItemDto(
                e.Id,
                e.Title,
                e.VenueName,
                e.LocationCity,
                e.StartTime,
                e.EndTime,
                e.Status.ToString(),
                e.HeroImageUrl
            ))
            .ToListAsync(ct);

        return Ok(new
        {
            page,
            pageSize,
            total,
            items
        });
    }

    [HttpGet("{id:long}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "id" })]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var e = await _db.Events.AsNoTracking()
            .Where(ev => ev.Id == id && ev.Status == EventStatus.Published)
            .Select(ev => new EventDetailDto(
                ev.Id,
                ev.Title,
                ev.Description,
                ev.VenueName,
                ev.LocationCity,
                ev.LocationAddress,
                ev.StartTime,
                ev.EndTime,
                ev.Status.ToString(),
                ev.HeroImageUrl,
                ev.EventCategories.Select(ec => ec.CategoryId).ToArray()
            ))
            .FirstOrDefaultAsync(ct);

        return e is null ? NotFound() : Ok(e);
    }

    [HttpGet("{id:long}/ticket-types")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "id", "includeAll" })]
    public async Task<IActionResult> TicketTypes(long id, [FromQuery] bool includeAll = false,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var ev = await _db.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.Status == EventStatus.Published, ct);
        if (ev is null) return NotFound();

        var tts = _db.TicketTypes.AsNoTracking().Where(t => t.EventId == id);

        if (!includeAll)
            tts = tts.Where(t => t.SalesStart <= now && now <= t.SalesEnd);

        var result = await tts
            .OrderBy(t => t.PriceCents)
            .Select(t => new TicketTypePublicDto(
                t.Id, t.Name, t.PriceCents, t.Currency,
                t.TotalQuantity, t.SoldQuantity,
                t.SalesStart, t.SalesEnd, t.PerOrderLimit
            ))
            .ToListAsync(ct);

        return Ok(result);
    }

    [HttpGet("{eventId:long}/discounts")]
    public async Task<IActionResult> ListPublicDiscounts(long eventId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var promos = await _db.Discounts.AsNoTracking()
            .Where(d => d.EventId == eventId && d.IsActive
                                             && (d.StartsAt == null || d.StartsAt <= now)
                                             && (d.EndsAt == null || d.EndsAt >= now)
                                             && (d.MaxUses == null || d.UsedCount < d.MaxUses))
            .Select(d => new
            {
                type = d.Type.ToString(), value = d.Value,
                scope = d.Scope.ToString(),
                endsAt = d.EndsAt
            })
            .ToListAsync(ct);

        return Ok(promos);
    }

    [HttpGet("categories/all")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Slug })
            .ToListAsync(ct);

        return Ok(items);
    }
}