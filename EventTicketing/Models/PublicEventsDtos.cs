namespace EventTicketing.Models;

public record EventListItemDto(
    long Id,
    string Title,
    string VenueName,
    string? LocationCity,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    string? ImageUrl  // Changed from HeroImageUrl to ImageUrl
);

public record EventDetailDto(
    long Id,
    string Title,
    string? Description,
    string VenueName,
    string? LocationCity,
    string? LocationAddress,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    string? ImageUrl,  // Changed from HeroImageUrl to ImageUrl
    int[] CategoryIds
);

// Keep your existing TicketTypePublicDto
public record TicketTypePublicDto(
    long Id,
    string Name,
    int PriceCents,
    string Currency,
    int TotalQuantity,
    int SoldQuantity,
    DateTime SalesStart,
    DateTime SalesEnd,
    int? PerOrderLimit
);