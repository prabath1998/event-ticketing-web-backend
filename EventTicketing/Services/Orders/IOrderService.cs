using EventTicketing.DTOs;
using EventTicketing.Entities;

namespace EventTicketing.Services.Orders;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(long userId, CreateOrderDto dto, CancellationToken ct);
    Task<bool> UserOwnsOrderAsync(long userId, long orderId, CancellationToken ct);
}