using EventTicketing.Entities;

namespace EventTicketing.Services.Payments;

public class PaymentInitResult
{
    public string Provider { get; set; } = default!;
    public string? ClientSecret { get; set; }
    public string? RedirectUrl { get; set; }
}

public interface IPaymentGateway
{
    string Name { get; }
   
    Task<PaymentInitResult> CreatePaymentAsync(Order order, CancellationToken ct);
    
    Task<(long orderId, bool success)> HandleWebhookAsync(string payload, string? signature, CancellationToken ct);
}