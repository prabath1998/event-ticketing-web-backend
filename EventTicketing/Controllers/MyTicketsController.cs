using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Enums;

namespace EventTicketing.Controllers;

[ApiController]
[Authorize]
[Route("me/tickets")]
public class MyTicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    public MyTicketsController(AppDbContext db, IConfiguration config)
    { _db = db; _config = config; }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? eventId, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var q = _db.Tickets.AsNoTracking()
            .Where(t => t.OrderItem.Order.UserId == userId);

        if (eventId is not null)
            q = q.Where(t => t.OrderItem.EventId == eventId.Value);

        var items = await q
            .OrderByDescending(t => t.IssuedAt)
            .Select(t => new TicketPublicDto(
                t.Id, t.OrderItemId, t.TicketCode, t.Status.ToString(),
                t.OrderItem.EventId, t.OrderItem.Event.Title, t.IssuedAt))
            .ToListAsync(ct);

        return Ok(items);
    }
   
    [HttpGet("{id:long}/qr")]
    public async Task<IActionResult> Qr(long id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var t = await _db.Tickets
            .Include(x => x.OrderItem).ThenInclude(oi => oi.Order)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (t is null) return NotFound();
        if (t.OrderItem.Order.UserId != userId) return Forbid();
       
        var payload = t.QrPayload ?? t.TicketCode;

        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(6); 

        return File(bytes, "image/png");
    }
}
