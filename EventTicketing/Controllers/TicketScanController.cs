using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Enums;

namespace EventTicketing.Controllers;

[ApiController]
[Route("tickets")]
[Authorize(Roles = "Organizer")]
public class TicketScanController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public TicketScanController(AppDbContext db, IConfiguration config)
    { _db = db; _config = config; }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    private async Task<long?> GetMyOrganizerIdAsync(long userId, CancellationToken ct)
        => await _db.OrganizerProfiles.Where(o => o.UserId == userId)
            .Select(o => (long?)o.Id).FirstOrDefaultAsync(ct);

    private bool VerifySignature(string payloadSigned)
    {
        var parts = payloadSigned.Split('|', 2);
        if (parts.Length != 2) return false;
        var data = parts[0];
        var sig = parts[1];

        var secret = _config["Tickets:QrSecret"] ?? "CHANGE_ME";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
       
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sig), Encoding.UTF8.GetBytes(expected));
    }

    private string ExtractTicketCode(string codeOrQr)
    {
        if (codeOrQr.Contains('|') && codeOrQr.Contains(':'))
        {
            var left = codeOrQr.Split('|', 2)[0];
            var lastColon = left.LastIndexOf(':');
            if (lastColon > 0) return left[(lastColon + 1)..];
        }
        return codeOrQr;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateTicketRequest req, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null) return Forbid();

        var isSigned = req.CodeOrQr.Contains('|');
        if (isSigned && !VerifySignature(req.CodeOrQr))
            return Ok(new ValidateTicketResponse(false, "Invalid", null, null, "Invalid signature"));

        var ticketCode = ExtractTicketCode(req.CodeOrQr);

        var t = await _db.Tickets
            .Include(x => x.OrderItem).ThenInclude(oi => oi.Event)
            .FirstOrDefaultAsync(x => x.TicketCode == ticketCode, ct);

        if (t is null)
            return Ok(new ValidateTicketResponse(false, "Invalid", null, null, "Ticket not found"));

        if (t.OrderItem.Event.OrganizerId != organizerId.Value)
            return Ok(new ValidateTicketResponse(false, "Forbidden", null, null, "Not your event"));

        return Ok(new ValidateTicketResponse(
            true, t.Status.ToString(), t.Id, t.OrderItem.EventId, null));
    }
    
    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInTicketRequest req, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null) return Forbid();

        var isSigned = req.CodeOrQr.Contains('|');
        if (isSigned && !VerifySignature(req.CodeOrQr))
            return Ok(new CheckInTicketResponse(false, "Invalid", null, null, "Invalid signature"));

        var ticketCode = ExtractTicketCode(req.CodeOrQr);
       
        var t = await _db.Tickets
            .Include(x => x.OrderItem).ThenInclude(oi => oi.Event)
            .FirstOrDefaultAsync(x => x.TicketCode == ticketCode, ct);

        if (t is null)
            return Ok(new CheckInTicketResponse(false, "Invalid", null, null, "Ticket not found"));

        if (t.OrderItem.Event.OrganizerId != organizerId.Value)
            return Ok(new CheckInTicketResponse(false, "Forbidden", null, null, "Not your event"));

        if (t.Status == TicketStatus.CheckedIn)
            return Ok(new CheckInTicketResponse(true, "CheckedIn", t.Id, t.CheckedInAt, "Already checked in"));

        if (t.Status != TicketStatus.Valid)
            return Ok(new CheckInTicketResponse(false, t.Status.ToString(), t.Id, null, "Ticket not valid for entry"));
       
        t.Status = TicketStatus.CheckedIn;
        t.CheckedInAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new CheckInTicketResponse(true, t.Status.ToString(), t.Id, t.CheckedInAt, null));
    }
}
