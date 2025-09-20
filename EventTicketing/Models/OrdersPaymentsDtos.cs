namespace EventTicketing.DTOs;

public record CartItemDto(long TicketTypeId, int Quantity);

public record CreateOrderDto(
    List<CartItemDto> Items,
    string? DiscountCode,
    string BuyerName,
    string BuyerEmail
);

public record OrderSummaryDto(
    long Id,
    string OrderNumber,
    string Status,
    int SubtotalCents,
    int DiscountCents,
    int FeesCents,
    int TotalCents,
    string Currency,
    DateTime CreatedAt
);

public record ApplyDiscountDto(string Code);

public record PaymentWebhookDto(
    string Provider,
    string Payload,
    string? Signature
);

public record OrderTotalsDto(
    int SubtotalCents,
    int DiscountCents,
    int FeesCents,
    int TotalCents,
    string Currency
);


public record PaymentInitResponseDto(
    string provider,
    string? clientSecret,
    string? redirectUrl,
    string? sessionId,
    bool requiresRedirect,
    long orderId
);