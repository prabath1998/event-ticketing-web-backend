using System.Threading;
using System.Threading.Tasks;

namespace EventTicketing.Services.Tickets
{
    public interface ITicketService
    {
        /// <summary>
        /// Issues tickets for a paid order. Idempotent: will not duplicate if tickets already exist.
        /// </summary>
        Task IssueForPaidOrderAsync(long orderId, CancellationToken ct = default);
    }
}