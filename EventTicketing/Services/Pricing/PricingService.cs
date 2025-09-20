using EventTicketing.Data;
using EventTicketing.Enums;
using EventTicketing.Entities;
using Microsoft.EntityFrameworkCore;
using EventTicketing.DTOs;
using EventTicketing.Services.Pricing;

public class PricingService : IPricingService
{
    private readonly AppDbContext _db;
    public PricingService(AppDbContext db) => _db = db;

    public async Task<PriceResult> ComputeAsync(List<CartItemDto> items, string? discountCode, CancellationToken ct)
    {
        if (items.Count == 0) throw new ArgumentException("Cart is empty.");

        var ttIds = items.Select(i => i.TicketTypeId).Distinct().ToList();
        var types = await _db.TicketTypes.AsNoTracking()
                     .Where(t => ttIds.Contains(t.Id))
                     .Select(t => new { t.Id, t.EventId, t.PriceCents })
                     .ToListAsync(ct);

        if (types.Count != ttIds.Count) throw new InvalidOperationException("One or more ticket types not found.");
       
        var eventId = types.Select(t => t.EventId).Distinct().Single();
       
        var subtotal = items.Sum(i => (types.First(t => t.Id == i.TicketTypeId).PriceCents) * i.Quantity);

        var discountCents = 0;

        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            var norm = discountCode.Trim().ToUpperInvariant();
            var now = DateTime.UtcNow;

            var d = await _db.Discounts.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                       x.EventId == eventId
                    && x.Code == norm
                    && x.IsActive
                    && (x.StartsAt == null || x.StartsAt <= now)
                    && (x.EndsAt == null || x.EndsAt >= now)
                    && (x.MaxUses == null || x.UsedCount < x.MaxUses)
                , ct);

            if (d != null && (d.MinSubtotalCents == null || subtotal >= d.MinSubtotalCents.Value))
            {
                if (d.Scope == DiscountScope.TicketType && d.TicketTypeId.HasValue)
                {
                    var targetQty = items.Where(i => i.TicketTypeId == d.TicketTypeId.Value).Sum(i => i.Quantity);
                    if (targetQty > 0)
                    {
                        var price = types.First(t => t.Id == d.TicketTypeId.Value).PriceCents;
                        var scopeSubtotal = price * targetQty;
                        discountCents = d.Type == DiscountType.Percentage
                            ? (int)Math.Floor(scopeSubtotal * (d.Value / 100.0))
                            : Math.Min(d.Value, scopeSubtotal);
                    }
                }
                else
                {
                    discountCents = d.Type == DiscountType.Percentage
                        ? (int)Math.Floor(subtotal * (d.Value / 100.0))
                        : Math.Min(d.Value, subtotal);
                }
            }
        }

        var fees = (int)Math.Round(subtotal * 0.025) + 50;
        var total = Math.Max(0, subtotal - discountCents + fees);

        return new PriceResult
        {
            SubtotalCents = subtotal,
            DiscountCents = discountCents,
            FeesCents = fees,
            Currency = "LKR", 
        };
    }
}
