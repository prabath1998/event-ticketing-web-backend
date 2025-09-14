using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;

namespace EventTicketing.Services.Pricing;

public class PricingService : IPricingService
{
    private readonly AppDbContext _db;
    public PricingService(AppDbContext db) => _db = db;

    public async Task<PriceResult> ComputeAsync(List<CartItemDto> items, string? discountCode, CancellationToken ct)
    {
        if (items.Count == 0) throw new ArgumentException("Cart is empty.");
        var ttIds = items.Select(i => i.TicketTypeId).ToList();
        var types = await _db.TicketTypes.AsNoTracking()
                        .Where(t => ttIds.Contains(t.Id))
                        .ToListAsync(ct);

        var subtotal = 0;
        foreach (var i in items)
        {
            var tt = types.FirstOrDefault(t => t.Id == i.TicketTypeId)
                     ?? throw new InvalidOperationException("Ticket type not found.");
            subtotal += tt.PriceCents * i.Quantity;
        }
       
        var discount = 0;
        if (!string.IsNullOrWhiteSpace(discountCode))
        {
            var active = await _db.Discounts.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Code == discountCode && d.IsActive
                                          && (d.StartsAt == null || d.StartsAt <= DateTime.UtcNow)
                                          && (d.EndsAt == null || d.EndsAt >= DateTime.UtcNow), ct);
            if (active != null)
            {
                discount = active.Type.ToString() == "Percentage"
                    ? (int)Math.Floor(subtotal * (active.Value / 100.0))
                    : Math.Min(active.Value, subtotal);
            }
        }
       
        var fees = (int)Math.Round(subtotal * 0.025) + 50;

        return new PriceResult
        {
            SubtotalCents = subtotal,
            DiscountCents = discount,
            FeesCents = fees,
            Currency = "LKR"
        };
    }
}
