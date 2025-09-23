using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.Models;
using EventTicketing.Entities;
using EventTicketing.Enums;
using iTextSharp.text;
using iTextSharp.text.pdf;
using EventTicketing.Utils;


namespace EventTicketing.Controllers;

[ApiController]
[Route("organizer/reports")]
//[Authorize(Roles = "Organizer")]
public class OrganizerReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrganizerReportsController(AppDbContext db) => _db = db;

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

    private long? GetUserId() =>
        long.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    private async Task<long?> GetOrganizerId(long userId, CancellationToken ct) =>
        await _db.OrganizerProfiles.Where(o => o.UserId == userId).Select(o => (long?)o.Id).FirstOrDefaultAsync(ct);

    [HttpGet("events")]
    public async Task<IActionResult> GetEventReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null) return Forbid();

        var query = _db.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == organizerId.Value);

        if (startDate.HasValue)
            query = query.Where(e => e.StartTime >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.StartTime <= endDate.Value);

        var events = await query
            .Select(e => new EventReportDto
            {
                Id = e.Id,
                Title = e.Title,
                VenueName = e.VenueName,
                LocationCity = e.LocationCity,
                Status = e.Status,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                CreatedAt = e.CreatedAt,
                TotalTicketsSold = e.TicketTypes.Sum(tt => tt.SoldQuantity),
                TotalCapacity = e.TicketTypes.Sum(tt => tt.TotalQuantity),
                TotalRevenue = e.TicketTypes
                    .SelectMany(tt => tt.OrderItems)
                    .Sum(oi => (decimal)oi.LineTotalCents) / 100m,
                Currency = "USD"
            })
            .OrderByDescending(e => e.StartTime)
            .ToListAsync(ct);

        return Ok(events);
    }

    [HttpPost("generate-pdf")]
    public async Task<IActionResult> GeneratePdfReport(OrganizerReportRequestDto request,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null) return Forbid();

        byte[] pdfBytes;
        string fileName;
        string reportTitle;

        using var memoryStream = new MemoryStream();

        var document = new Document(PageSize.A4, 50, 50, 25, 25);
        var writer = PdfWriter.GetInstance(document, memoryStream);

        document.Open();

        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.DARK_GRAY);
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.DARK_GRAY);
        var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);

        if (request.ReportType.ToLower() == "users")
        {
            reportTitle = "User Report";
            fileName = $"user-report-{DateTime.Now:yyyy-MM-dd}.pdf";

            document.Add(new Paragraph(reportTitle, titleFont));
            document.Add(new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}", normalFont));
            document.Add(new Paragraph(" "));

            var table = new PdfPTable(6);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 1, 3, 3, 2, 2, 2 });

            table.AddCell(new PdfPCell(new Phrase("ID", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Email", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Name", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Tickets", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Spent", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Joined", headerFont)));
            document.Add(table);
        }
        else if (request.ReportType.ToLower() == "events")
        {
            reportTitle = "Event Report";
            fileName = $"event-report-{DateTime.Now:yyyy-MM-dd}.pdf";

            document.Add(new Paragraph(reportTitle, titleFont));
            document.Add(new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}", normalFont));
            document.Add(new Paragraph(" "));

            var events = await GetEventReportData(organizerId.Value, request.StartDate, request.EndDate, ct);


            var table = new PdfPTable(7);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 1, 3, 2, 2, 2, 2, 2 });

            table.AddCell(new PdfPCell(new Phrase("ID", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Title", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Venue", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Status", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Sold", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Capacity", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Revenue", headerFont)));

            foreach (var evt in events)
            {
                table.AddCell(new PdfPCell(new Phrase(evt.Id.ToString(), normalFont)));
                table.AddCell(new PdfPCell(new Phrase(evt.Title, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(evt.VenueName, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(evt.Status.ToString(), normalFont)));
                table.AddCell(new PdfPCell(new Phrase(evt.TotalTicketsSold.ToString(), normalFont)));
                table.AddCell(new PdfPCell(new Phrase(evt.TotalCapacity.ToString(), normalFont)));
                table.AddCell(new PdfPCell(new Phrase($"${evt.TotalRevenue:F2}", normalFont)));
            }

            document.Add(table);
        }
        else
        {
            return BadRequest("Invalid report type. Use 'users' or 'events'.");
        }

        document.Close();
        pdfBytes = memoryStream.ToArray();

        return File(pdfBytes, "application/pdf", fileName);
    }

    private async Task<List<EventReportDto>> GetEventReportData(long organizerId, DateTime? startDate,
        DateTime? endDate, CancellationToken ct)
    {
        var query = _db.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == organizerId);

        if (startDate.HasValue)
            query = query.Where(e => e.StartTime >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.StartTime <= endDate.Value);

        return await query
            .Select(e => new EventReportDto
            {
                Id = e.Id,
                Title = e.Title,
                VenueName = e.VenueName,
                LocationCity = e.LocationCity,
                Status = e.Status,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                CreatedAt = e.CreatedAt,
                TotalTicketsSold = e.TicketTypes.Sum(tt => tt.SoldQuantity),
                TotalCapacity = e.TicketTypes.Sum(tt => tt.TotalQuantity),
                TotalRevenue = e.TicketTypes
                    .SelectMany(tt => tt.OrderItems)
                    .Sum(oi => (decimal)oi.LineTotalCents) / 100m,
                Currency = "USD"
            })
            .OrderByDescending(e => e.StartTime)
            .ToListAsync(ct);
    }
    
    public sealed record OrganizerSalesRow(
        long OrderId,
        string OrderNumber,
        long EventId,
        string EventTitle,
        string TicketType,
        int Quantity,
        int UnitPriceCents,
        string? DiscountCode,
        int DiscountCents,
        int FeesCents,
        int TotalCents,
        string Currency,
        DateTime? PaidAtUtc
    );

    [HttpGet("sales.csv")]
    public async Task<IActionResult> SalesCsv(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] long? eventId,
        CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid is null) return Unauthorized();
        var orgId = await GetOrganizerId(uid.Value, ct);
        if (orgId is null) return Forbid();

        var q = _db.OrderItems
            .AsNoTracking()
            .Include(oi => oi.Event)
            .Include(oi => oi.TicketType)
            .Include(oi => oi.Order).ThenInclude(o => o.Payment)
            .Where(oi => oi.Event.OrganizerId == orgId
                         && oi.Order.Status == EventTicketing.Enums.OrderStatus.Paid);

        if (eventId.HasValue) q = q.Where(oi => oi.EventId == eventId.Value);
        if (from.HasValue) q = q.Where(oi => oi.Order.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(oi => oi.Order.CreatedAt < to.Value);
      
        var rows = await q
            .OrderByDescending(oi => oi.OrderId)
            .Select(oi => new OrganizerSalesRow(
                oi.OrderId,
                oi.Order.OrderNumber,
                oi.EventId,
                oi.Event.Title,
                oi.TicketType.Name,
                oi.Quantity,
                oi.UnitPriceCents,
                oi.Order.DiscountCode,
                oi.Order.DiscountCents,
                oi.Order.FeesCents,
                oi.Order.TotalCents,
                oi.Order.Currency,
                oi.Order.Payment != null ? oi.Order.Payment.PaidAt : null
            ))
            .ToListAsync(ct);

        var csv = Csv.Write(rows);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv);
        return File(bytes, "text/csv", $"organizer-sales-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }


    [HttpGet("revenue.csv")]
    public async Task<IActionResult> RevenueCsv(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid is null) return Unauthorized();
        var orgId = await GetOrganizerId(uid.Value, ct);
        if (orgId is null) return Forbid();

        var q = _db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.TicketType)
            .Include(o => o.Items).ThenInclude(i => i.Event)
            .Where(o => o.Items.Any(i => i.Event.OrganizerId == orgId) &&
                        o.Status == EventTicketing.Enums.OrderStatus.Paid);

        if (from.HasValue) q = q.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(o => o.CreatedAt < to.Value);

        var rows = new List<string[]>
        {
            new[] { "EventId", "EventTitle", "TicketsSold", "GrossCents", "DiscountCents", "FeesCents", "NetCents" }
        };

        var data = await q
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.EventId, i.Event.Title })
            .Select(g => new
            {
                g.Key.EventId, EventTitle = g.Key.Title,
                TicketsSold = g.Sum(x => x.Quantity),
                Gross = g.Sum(x => x.Quantity * x.UnitPriceCents),
            })
            .Join(_db.Orders.Where(o => o.Status == EventTicketing.Enums.OrderStatus.Paid),
                g => g.EventId,
                o => o.Items.First().EventId, // safe because paid orders have at least one item
                (g, o) => new { g, o })
            .GroupBy(x => new { x.g.EventId, x.g.EventTitle, x.g.TicketsSold, x.g.Gross })
            .Select(grp => new
            {
                grp.Key.EventId,
                grp.Key.EventTitle,
                grp.Key.TicketsSold,
                Gross = grp.Key.Gross,
                Discount = grp.Sum(x => x.o.DiscountCents),
                Fees = grp.Sum(x => x.o.FeesCents),
                Net = grp.Sum(x => x.o.TotalCents)
            })
            .OrderByDescending(x => x.Net)
            .ToListAsync(ct);

        rows.AddRange(data.Select(d => new[]
        {
            d.EventId.ToString(), d.EventTitle, d.TicketsSold.ToString(),
            d.Gross.ToString(), d.Discount.ToString(), d.Fees.ToString(), d.Net.ToString()
        }));


        var csv = Csv.Write(rows);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv",
            $"organizer-revenue-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }
}