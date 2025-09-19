using EventTicketing.Enums;

namespace EventTicketing.Models;

public class UserReportDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int TotalTicketsPurchased { get; set; }
    public decimal TotalAmountSpent { get; set; }
    public string Currency { get; set; } = "USD";
}

public class EventReportDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public string LocationCity { get; set; } = string.Empty;
    public EventStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalTicketsSold { get; set; }
    public int TotalCapacity { get; set; }
    public decimal TotalRevenue { get; set; }
    public string Currency { get; set; } = "USD";
}

public class OrganizerReportRequestDto
{
    public string ReportType { get; set; } = string.Empty; // "users" or "events"
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public long? EventId { get; set; }
}
