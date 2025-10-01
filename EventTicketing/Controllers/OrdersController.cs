using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Enums;
using EventTicketing.Services.Orders;
using EventTicketing.Services.Pricing;

namespace EventTicketing.Controllers;

[ApiController]
[Authorize]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOrderService _orders;
    private readonly IPricingService _pricing;

    public OrdersController(AppDbContext db, IOrderService orders, IPricingService pricing)
    {
        _db = db;
        _orders = orders;
        _pricing = pricing;
    }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var order = await _orders.CreateOrderAsync(userId, dto, ct);

        var summary = new OrderSummaryDto(
            order.Id, order.OrderNumber, order.Status.ToString(),
            order.SubtotalCents, order.DiscountCents, order.FeesCents, order.TotalCents,
            order.Currency, order.CreatedAt
        );
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, summary);
    }

    [HttpGet("me")]
    public async Task<IActionResult> ListMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var q = _db.Orders.AsNoTracking().Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderSummaryDto(
                o.Id, o.OrderNumber, o.Status.ToString(),
                o.SubtotalCents, o.DiscountCents, o.FeesCents, o.TotalCents,
                o.Currency, o.CreatedAt))
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await _orders.UserOwnsOrderAsync(userId, id, ct)) return Forbid();

        var o = await _db.Orders.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(o => new
            {
                Order = new OrderSummaryDto(
                    o.Id, o.OrderNumber, o.Status.ToString(),
                    o.SubtotalCents, o.DiscountCents, o.FeesCents, o.TotalCents,
                    o.Currency, o.CreatedAt
                ),
                Items = o.Items.Select(i => new
                {
                    i.Id, i.EventId, i.TicketTypeId, i.UnitPriceCents, i.Quantity, i.LineTotalCents
                }).ToList(),
                Payment = o.Payment == null
                    ? null
                    : new
                    {
                        o.Payment.Id, o.Payment.Provider, o.Payment.Status, o.Payment.AmountCents, o.Payment.Currency,
                        o.Payment.PaidAt
                    }
            })
            .FirstOrDefaultAsync(ct);

        return o is null ? NotFound() : Ok(o);
    }

    [HttpGet("{orderId:long}/summary")]
    public async Task<ActionResult<OrderTotalsDto>> GetSummary(long orderId, CancellationToken ct)
    {
        var o = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o == null) return NotFound();

        var total = o.SubtotalCents - o.DiscountCents + o.FeesCents;
        return new OrderTotalsDto(o.SubtotalCents, o.DiscountCents, o.FeesCents, total, o.Currency);
    }

    [HttpPost("{orderId:long}/apply-discount")]
    public async Task<ActionResult<OrderTotalsDto>> ApplyDiscount(
        long orderId, [FromBody] ApplyDiscountDto body, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.TicketType)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null) return NotFound();
        if (order.Status != OrderStatus.Pending) return BadRequest("Order not pending.");

        var codeRaw = body.Code?.Trim();
        if (string.IsNullOrWhiteSpace(codeRaw))
            return BadRequest("Promo code is required.");
        var code = codeRaw.ToUpperInvariant();
      
        var eventId = order.Items
            .Select(i => i.TicketType.EventId)
            .Distinct()
            .SingleOrDefault();

        if (eventId == 0)
            return BadRequest("Order items are invalid for discount application.");

        var now = DateTime.UtcNow;
       
        var discount = await _db.Discounts.AsNoTracking()
            .FirstOrDefaultAsync(d =>
                d.EventId == eventId &&
                d.Code == code &&
                d.IsActive &&
                (d.StartsAt == null || d.StartsAt <= now) &&
                (d.EndsAt == null || d.EndsAt >= now) &&
                (d.MaxUses == null || d.UsedCount < d.MaxUses), ct);

        if (discount == null)
            return BadRequest("Invalid or expired promo code.");
       
        if (discount.Scope == DiscountScope.TicketType)
        {
            var containsTicketType = order.Items.Any(i => i.TicketTypeId == discount.TicketTypeId);
            if (!containsTicketType)
                return BadRequest("Promo code does not apply to selected ticket types.");
        }
        
        var currentSubtotal = order.Items.Sum(i => i.Quantity * i.UnitPriceCents);
        if (discount.MinSubtotalCents.HasValue && currentSubtotal < discount.MinSubtotalCents.Value)
            return BadRequest("Promo code requires a higher subtotal.");
       
        var items = order.Items
            .Select(i => new CartItemDto(i.TicketTypeId, i.Quantity))
            .ToList();

        var result = await _pricing.ComputeAsync(items, code, ct);
       
        if (result.DiscountCents <= 0)
            return BadRequest("Promo code is not applicable to this order.");

        order.SubtotalCents = result.SubtotalCents;
        order.DiscountCents = result.DiscountCents;
        order.FeesCents = result.FeesCents;
        order.TotalCents = result.SubtotalCents - result.DiscountCents + result.FeesCents;
        order.Currency = result.Currency;
        order.DiscountCode = code;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new OrderTotalsDto(
            order.SubtotalCents, order.DiscountCents, order.FeesCents, order.TotalCents, order.Currency);
    }


    [HttpDelete("{orderId:long}/discount")]
    public async Task<ActionResult<OrderTotalsDto>> RemoveDiscount(long orderId, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.TicketType)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null) return NotFound();
        if (order.Status != OrderStatus.Pending) return BadRequest("Order not pending.");

        var items = order.Items
            .Select(i => new CartItemDto(i.TicketTypeId, i.Quantity))
            .ToList();

        var result = await _pricing.ComputeAsync(items, null, ct);

        order.SubtotalCents = result.SubtotalCents;
        order.DiscountCents = 0;
        order.FeesCents = result.FeesCents;
        order.TotalCents = result.SubtotalCents - 0 + result.FeesCents;
        order.Currency = result.Currency;
        order.DiscountCode = null;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new OrderTotalsDto(order.SubtotalCents, order.DiscountCents, order.FeesCents, order.TotalCents,
            order.Currency);
    }
}