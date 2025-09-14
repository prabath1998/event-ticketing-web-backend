namespace EventTicketing.Models;

public record CreateEventDto(
    string Title,
    string VenueName,
    string? Description,
    string? LocationCity,
    string? LocationAddress,
    DateTime StartTime,
    DateTime EndTime,
    int[] CategoryIds,
    IFormFile? ImageFile  // Add image file support
);

public record UpdateEventDto(
    string Title,
    string VenueName,
    string? Description,
    string? LocationCity,
    string? LocationAddress,
    DateTime StartTime,
    DateTime EndTime,
    int[] CategoryIds,
    IFormFile? ImageFile  // Add image file support
);

// Keep your existing ticket type DTOs
public record CreateTicketTypeDto(
    string Name,
    int PriceCents,
    string Currency,
    int TotalQuantity,
    DateTime SalesStart,
    DateTime SalesEnd,
    int? PerOrderLimit
);

public record UpdateTicketTypeDto(
    string Name,
    int PriceCents,
    string Currency,
    int TotalQuantity,
    DateTime SalesStart,
    DateTime SalesEnd,
    int? PerOrderLimit
);