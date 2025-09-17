using EventTicketing.Entities;

namespace EventTicketing.Services.Payments;

public record PaymentSessionResult(
    string Provider,
    string? ClientSecret,
    string RedirectUrl
);


public interface IPaymentGateway
{
    string Name { get; }
    Task<PaymentSessionResult> CreatePaymentSessionAsync(long orderId, CancellationToken ct = default);
    Task<(long orderId, bool success)> HandleWebhookAsync(string payload, string? signature, CancellationToken ct = default);
}

