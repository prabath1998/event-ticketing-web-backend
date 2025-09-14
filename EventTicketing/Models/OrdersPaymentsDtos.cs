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

public record PaymentInitResponseDto(
    string Provider,
    string? ClientSecret,
    string? RedirectUrl
);

public record PaymentWebhookDto( 
    string Provider,
    string Payload,
    string? Signature
);