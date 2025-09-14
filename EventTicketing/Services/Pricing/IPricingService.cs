using EventTicketing.DTOs;

namespace EventTicketing.Services.Pricing;

public class PriceResult
{
    public int SubtotalCents { get; set; }
    public int DiscountCents { get; set; }
    public int FeesCents { get; set; }
    public int TotalCents => SubtotalCents - DiscountCents + FeesCents;
    public string Currency { get; set; } = "LKR";
}

public interface IPricingService
{
    Task<PriceResult> ComputeAsync(List<CartItemDto> items, string? discountCode, CancellationToken ct);
}