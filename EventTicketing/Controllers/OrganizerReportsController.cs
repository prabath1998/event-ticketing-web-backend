using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.Models;
using EventTicketing.Entities;
using EventTicketing.Enums;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace EventTicketing.Controllers;

[ApiController]
[Route("organizer/reports")]
[Authorize(Roles = "Organizer")]
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


    [HttpGet("events")]
    public async Task<IActionResult> GetEventReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct = default)
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
    public async Task<IActionResult> GeneratePdfReport(OrganizerReportRequestDto request, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var organizerId = await GetMyOrganizerIdAsync(userId, ct);
        if (organizerId is null) return Forbid();

        byte[] pdfBytes;
        string fileName;
        string reportTitle;

        using var memoryStream = new MemoryStream();

        // Create PDF document
        var document = new Document(PageSize.A4, 50, 50, 25, 25);
        var writer = PdfWriter.GetInstance(document, memoryStream);

        document.Open();

        // Add title
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

            // Get user data
            //var users = await GetUserReportData(organizerId.Value, request.StartDate, request.EndDate, request.EventId, ct);

            // Create table with 6 columns
            var table = new PdfPTable(6);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 1, 3, 3, 2, 2, 2 });

            // Add headers
            table.AddCell(new PdfPCell(new Phrase("ID", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Email", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Name", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Tickets", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Spent", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Joined", headerFont)));

            // Add data rows
            //foreach (var user in users)
            //{
            //    table.AddCell(new PdfPCell(new Phrase(user.Id.ToString(), normalFont)));
            //    table.AddCell(new PdfPCell(new Phrase(user.Email, normalFont)));
            //    table.AddCell(new PdfPCell(new Phrase($"{user.FirstName} {user.LastName}", normalFont)));
            //    table.AddCell(new PdfPCell(new Phrase(user.TotalTicketsPurchased.ToString(), normalFont)));
            //    table.AddCell(new PdfPCell(new Phrase($"$" + user.TotalAmountSpent.ToString("F2"), normalFont)));
            //    table.AddCell(new PdfPCell(new Phrase(user.CreatedAt.ToString("yyyy-MM-dd"), normalFont)));
            //}

            document.Add(table);
        }
        else if (request.ReportType.ToLower() == "events")
        {
            reportTitle = "Event Report";
            fileName = $"event-report-{DateTime.Now:yyyy-MM-dd}.pdf";

            document.Add(new Paragraph(reportTitle, titleFont));
            document.Add(new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}", normalFont));
            document.Add(new Paragraph(" "));

            // Get event data
            var events = await GetEventReportData(organizerId.Value, request.StartDate, request.EndDate, ct);

            // Create table
            var table = new PdfPTable(7);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 1, 3, 2, 2, 2, 2, 2 });

            // Add headers
            table.AddCell(new PdfPCell(new Phrase("ID", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Title", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Venue", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Status", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Sold", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Capacity", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Revenue", headerFont)));

            // Add data rows
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

    //private async Task<List<UserReportDto>> GetUserReportData(long organizerId, DateTime? startDate, DateTime? endDate, long? eventId, CancellationToken ct)
    //{
    //    var query = _db.Users
    //        .AsNoTracking()
    //        .Where(u => u.Orders.Any(o => o.Items.Any(oi =>
    //            oi.TicketType.Event.OrganizerId == organizerId)));

    //    // Apply date filters to the orders
    //    if (startDate.HasValue || endDate.HasValue || eventId.HasValue)
    //    {
    //        query = query.Where(u => u.Orders.Any(o =>
    //            (!startDate.HasValue || o.CreatedAt >= startDate.Value) &&
    //            (!endDate.HasValue || o.CreatedAt <= endDate.Value) &&
    //            o.Items.Any(oi =>
    //                oi.TicketType.Event.OrganizerId == organizerId &&
    //                (!eventId.HasValue || oi.TicketType.EventId == eventId.Value)
    //            )
    //        ));
    //    }

    //    return await query
    //        .Select(u => new UserReportDto
    //        {
    //            Id = u.Id,
    //            Email = u.Email,
    //            FirstName = u.FirstName,
    //            LastName = u.LastName,
    //            CreatedAt = u.CreatedAt
    //        })
    //        .OrderByDescending(u => u.Id)
    //        .ToListAsync(ct);
    //}

    private async Task<List<EventReportDto>> GetEventReportData(long organizerId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
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
}