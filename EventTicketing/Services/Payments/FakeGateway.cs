using EventTicketing.Data;
using EventTicketing.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Services.Payments;

public class FakeGateway : IPaymentGateway
{
    public string Name => "dummy";

    public Task<PaymentSessionResult> CreatePaymentSessionAsync(long orderId, CancellationToken ct = default)
    {
        var redirectUrl = $"http://localhost:3000/checkout/dummy?orderId={orderId}";

        return Task.FromResult(new PaymentSessionResult(
            Provider: Name,
            ClientSecret: null,
            RedirectUrl: redirectUrl
        ));
    }

    public Task<(long orderId, bool success)> HandleWebhookAsync(string payload, string? signature, CancellationToken ct = default)
    {
        return Task.FromResult((0L, false));
    }
}

