using EventTicketing.Entities;

namespace EventTicketing.Services.Tickets;

public interface ITicketService
{
    Task IssueForOrderAsync(long orderId, CancellationToken ct);
}