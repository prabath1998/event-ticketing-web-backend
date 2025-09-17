using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Enums;

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
}