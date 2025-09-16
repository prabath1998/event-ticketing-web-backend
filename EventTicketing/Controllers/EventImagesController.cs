using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.Entities;

namespace EventTicketing.Controllers;

[ApiController]
[Route("api/events")]
public class EventImagesController : ControllerBase
{
    private readonly AppDbContext _db;

    public EventImagesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:long}/image")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)] // Cache for 1 hour
    public async Task<IActionResult> GetEventImage(long id, CancellationToken ct)
    {
        var eventImage = await _db.Events
            .AsNoTracking()
            .Where(e => e.Id == id && e.ImageData != null)
            .Select(e => new { e.ImageData, e.ImageContentType })
            .FirstOrDefaultAsync(ct);

        if (eventImage == null || eventImage.ImageData == null)
        {
            return NotFound();
        }

        return File(eventImage.ImageData, eventImage.ImageContentType ?? "image/jpeg");
    }
}