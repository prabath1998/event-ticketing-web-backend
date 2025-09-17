namespace EventTicketing.DTOs;

public record TicketPublicDto(
    long Id, long OrderItemId, string TicketCode, string Status,
    long EventId, string EventTitle, DateTime IssuedAt);

public record ValidateTicketRequest(string CodeOrQr); 
public record ValidateTicketResponse(
    bool Valid, string Status, long? TicketId, long? EventId, string? Message);

public record CheckInTicketRequest(string CodeOrQr); 
public record CheckInTicketResponse(
    bool Success, string Status, long? TicketId, DateTime? CheckedInAt, string? Message);