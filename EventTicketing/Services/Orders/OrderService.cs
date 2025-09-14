using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Entities;
using EventTicketing.Enums;
using EventTicketing.Services.Pricing;

namespace EventTicketing.Services.Orders;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IPricingService _pricing;

    public OrderService(AppDbContext db, IPricingService pricing)
    {
        _db = db; _pricing = pricing;
    }

    public async Task<Order> CreateOrderAsync(long userId, CreateOrderDto dto, CancellationToken ct)
    {
        if (dto.Items is null || dto.Items.Count == 0) throw new InvalidOperationException("Cart is empty.");

       
        var ttIds = dto.Items.Select(i => i.TicketTypeId).ToList();
        var ticketTypes = await _db.TicketTypes
            .Where(t => ttIds.Contains(t.Id))
            .Include(t => t.Event)
            .ToListAsync(ct);
      
        var now = DateTime.UtcNow;
        foreach (var item in dto.Items)
        {
            var tt = ticketTypes.FirstOrDefault(t => t.Id == item.TicketTypeId)
                     ?? throw new InvalidOperationException("Ticket type not found.");
            if (!(tt.SalesStart <= now && now <= tt.SalesEnd))
                throw new InvalidOperationException($"Ticket type '{tt.Name}' not on sale.");
            if (item.Quantity <= 0) throw new InvalidOperationException("Invalid quantity.");
            if (tt.PerOrderLimit.HasValue && item.Quantity > tt.PerOrderLimit.Value)
                throw new InvalidOperationException($"Per-order limit exceeded for '{tt.Name}'.");
        }
        
        var price = await _pricing.ComputeAsync(dto.Items, dto.DiscountCode, ct);
      
        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = new Order
        {
            UserId = userId,
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            Status = OrderStatus.Pending,
            SubtotalCents = price.SubtotalCents,
            DiscountCents = price.DiscountCents,
            FeesCents = price.FeesCents,
            TotalCents = price.TotalCents,
            Currency = price.Currency,
            DiscountCode = dto.DiscountCode
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        foreach (var item in dto.Items)
        {
            var tt = ticketTypes.First(t => t.Id == item.TicketTypeId);
          
            if (tt.SoldQuantity + item.Quantity > tt.TotalQuantity)
                throw new InvalidOperationException($"Not enough stock for '{tt.Name}'.");
           
            tt.SoldQuantity += item.Quantity;
            _db.TicketTypes.Update(tt);

            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                EventId = tt.EventId,
                TicketTypeId = tt.Id,
                UnitPriceCents = tt.PriceCents,
                Quantity = item.Quantity,
                LineTotalCents = tt.PriceCents * item.Quantity,
                SnapshotJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    EventId = tt.EventId,
                    TicketTypeId = tt.Id,
                    tt.Name,
                    tt.PriceCents,
                    tt.Currency
                })
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return order;
    }

    public async Task<bool> UserOwnsOrderAsync(long userId, long orderId, CancellationToken ct)
        => await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == orderId && o.UserId == userId, ct);
}
