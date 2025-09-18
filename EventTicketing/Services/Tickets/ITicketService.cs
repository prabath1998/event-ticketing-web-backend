using System.Threading;
using System.Threading.Tasks;

namespace EventTicketing.Services.Tickets
{
    public interface ITicketService
    {
        Task IssueForPaidOrderAsync(long orderId, CancellationToken ct = default);
    }
}