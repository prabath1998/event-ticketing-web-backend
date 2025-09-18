namespace EventTicketing.Services.Payments;
using System.Threading;
using System.Threading.Tasks;

public record PaymentSessionResult(
    string Provider,
    string? ClientSecret,
    string? RedirectUrl,
    string? SessionId,
    bool RequiresRedirect
);

public interface IPaymentGateway
{
    string Name { get; }
    Task<PaymentSessionResult> CreatePaymentSessionAsync(long orderId, CancellationToken ct = default);
    Task<(long orderId, bool success)> HandleWebhookAsync(string payload, string? signature, CancellationToken ct = default);
}



