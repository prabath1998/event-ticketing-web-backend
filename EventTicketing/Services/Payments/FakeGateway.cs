using EventTicketing.Data;
using EventTicketing.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Services.Payments;

public class FakeGateway : IPaymentGateway
{
    public string Name => "FakePay";
    private readonly AppDbContext _db;
    public FakeGateway(AppDbContext db) { _db = db; }

    public Task<PaymentInitResult> CreatePaymentAsync(Order order, CancellationToken ct)
    {
        var redirect = $"https://payments.local/pay?order={order.OrderNumber}";
        return Task.FromResult(new PaymentInitResult
        {
            Provider = Name,
            RedirectUrl = redirect
        });
    }

    public async Task<(long orderId, bool success)> HandleWebhookAsync(string payload, string? signature, CancellationToken ct)
    {
        var parts = payload.Split(':', 2);
        if (parts.Length != 2) return (0, false);
        var orderNumber = parts[0];
        var success = parts[1].Equals("success", StringComparison.OrdinalIgnoreCase);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
        return order == null ? (0, false) : (order.Id, success);
    }
}