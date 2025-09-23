using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Enums;
using EventTicketing.Utils;



namespace EventTicketing.Controllers;

[ApiController]
[Route("admin/reports")]
[Authorize(Roles = "Admin")]
public class AdminReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminReportsController(AppDbContext db) => _db = db;
    
    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
    {
        var start = from ?? DateTime.UtcNow.AddMonths(-1);
        var end   = to   ?? DateTime.UtcNow;

        var totalUsers = await _db.Users.LongCountAsync(ct);
        var activeUsers = await _db.Users.LongCountAsync(u => u.IsActive, ct);

        var totalEvents = await _db.Events.LongCountAsync(ct);
        var publishedEvents = await _db.Events.LongCountAsync(e => e.Status == EventStatus.Published, ct);

        var ordersInRange = _db.Orders.AsNoTracking().Where(o => o.CreatedAt >= start && o.CreatedAt < end);
        var totalOrders = await ordersInRange.LongCountAsync(ct);
        var paidOrders = await ordersInRange.LongCountAsync(o => o.Status == OrderStatus.Paid, ct);

        var revenueCents = await ordersInRange.Where(o => o.Status == OrderStatus.Paid).SumAsync(o => (long?)o.TotalCents, ct) ?? 0;

        var ticketsIssued = await _db.Tickets.AsNoTracking()
            .Where(t => t.IssuedAt >= start && t.IssuedAt < end)
            .LongCountAsync(ct);

        var dto = new AdminOverviewDto(start, end, totalUsers, activeUsers, totalEvents, publishedEvents, totalOrders, paidOrders, ticketsIssued, revenueCents);
        return Ok(dto);
    }
    
   [HttpGet("users.csv")]
    public async Task<IActionResult> UsersCsv(CancellationToken ct)
    {
        var rows = await _db.Users
            .Select(u => new
            {
                UserId = u.Id,
                u.Email,
                CreatedAt = u.CreatedAt,                 
                Orders = _db.Orders.Count(o => o.UserId == u.Id),
                Tickets = _db.Tickets.Count(t => t.OrderItem.Order.UserId == u.Id),
                TotalSpentCents = _db.Payments
                    .Where(p => p.Order.UserId == u.Id && p.Status == PaymentStatus.Succeeded)
                    .Sum(p => (int?)p.AmountCents) ?? 0
            })
            .OrderByDescending(x => x.TotalSpentCents)
            .ToListAsync(ct);

        var csv = Csv.Write(rows);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "users.csv");
    }

    [HttpGet("events.csv")]
    public async Task<IActionResult> EventsCsv(CancellationToken ct)
    {
        var rows = await _db.Events
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.StartTime,
                e.EndTime,
                Status = e.Status.ToString(),
                TicketsTotal = _db.Tickets.Count(t => t.OrderItem.TicketType.EventId == e.Id),
                TicketsCheckedIn = _db.Tickets.Count(t =>
                    t.OrderItem.TicketType.EventId == e.Id && t.Status == TicketStatus.CheckedIn),
                Orders = _db.Orders.Count(o => o.Items.Any(i => i.TicketType.EventId == e.Id)),
                GrossCents = _db.Orders
                    .Where(o => o.Payment != null && o.Payment.Status == PaymentStatus.Succeeded)
                    .SelectMany(o => o.Items)
                    .Where(i => i.TicketType.EventId == e.Id)
                    .Sum(i => (long?)i.Quantity * i.UnitPriceCents) ?? 0
            })
            .OrderByDescending(x => x.GrossCents)
            .ToListAsync(ct);

        var csv = Csv.Write(rows);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "events.csv");
    }
    
    [HttpGet("sales.csv")]
    public async Task<IActionResult> SalesCsv(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
    {
        var q = _db.Payments.AsNoTracking();

        if (from.HasValue) q = q.Where(p => p.PaidAt >= from.Value);
        if (to.HasValue) q = q.Where(p => p.PaidAt < to.Value);

        var rows = await q
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new
            {
                p.Id,
                p.OrderId,
                UserId = p.Order.UserId,
                p.AmountCents,
                p.Currency,
                Status = p.Status.ToString(),
                p.PaidAt
            })
            .ToListAsync(ct);

        var csv = Csv.Write(rows);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "sales.csv");
    }
}