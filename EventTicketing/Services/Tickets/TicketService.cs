using System.Security.Cryptography;
using System.Text;
using EventTicketing.Data;
using EventTicketing.Entities;
using EventTicketing.Enums;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Services.Tickets
{
    public class TicketService : ITicketService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public TicketService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task IssueForPaidOrderAsync(long orderId, CancellationToken ct = default)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order is null)
                throw new InvalidOperationException($"Order {orderId} not found.");

            if (order.Status != OrderStatus.Pending)
                throw new InvalidOperationException($"Order {orderId} not paid. Current status: {order.Status}");
           
            var alreadyHasTickets = await _db.Tickets
                .AnyAsync(t => order.Items.Select(i => i.Id).Contains(t.OrderItemId), ct);
            if (alreadyHasTickets) return;

            var now = DateTime.UtcNow;

            foreach (var item in order.Items)
            {
                var qty = Math.Max(0, item.Quantity);
                for (var i = 0; i < qty; i++)
                {
                    var code = GenerateTicketCode(); 
                    var payload = BuildSignedQrPayload(order.Id, item.Id, code);

                    var ticket = new Ticket
                    {
                        OrderItemId = item.Id,
                        TicketCode = code,
                        QrPayload  = payload,
                        Status     = TicketStatus.Valid,
                        IssuedAt   = now
                    };

                    _db.Tickets.Add(ticket);
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        private string GenerateTicketCode(int bytes = 6)
        {
            Span<byte> b = stackalloc byte[bytes];
            RandomNumberGenerator.Fill(b);
            var s = Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            return $"TKT-{s}";
        }

        private string BuildSignedQrPayload(long orderId, long orderItemId, string ticketCode)
        {
            var data = $"{orderId}:{orderItemId}:{ticketCode}";
            var sig  = Sign(data);
            return $"{data}|{sig}";
        }

        private string Sign(string data)
        {
            var secret = _config["Tickets:QrSecret"] ?? "CHANGE_ME";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }
    }
}
