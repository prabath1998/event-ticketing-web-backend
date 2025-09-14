using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.Entities;
using EventTicketing.Enums;
using System.Security.Cryptography;
using System.Text;

namespace EventTicketing.Services.Tickets;

public class TicketService : ITicketService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    public TicketService(AppDbContext db, IConfiguration config)
    {
        _db = db; _config = config;
    }

    public async Task IssueForOrderAsync(long orderId, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null) throw new InvalidOperationException("Order not found.");
        if (order.Status != OrderStatus.Paid) throw new InvalidOperationException("Order not paid.");

        var toCreate = new List<Ticket>();

        foreach (var item in order.Items)
        {
            for (int i = 0; i < item.Quantity; i++)
            {
                var code = $"TKT-{Guid.NewGuid().ToString("N")[..10].ToUpper()}";
                var payload = SignPayload($"{order.Id}:{item.Id}:{code}");
                toCreate.Add(new Ticket
                {
                    OrderItemId = item.Id,
                    TicketCode = code,
                    QrPayload = payload,
                    Status = TicketStatus.Valid
                });
            }
        }

        _db.Tickets.AddRange(toCreate);
        await _db.SaveChangesAsync(ct);
    }

    private string SignPayload(string data)
    {
        var secret = _config["Tickets:QrSecret"] ?? "CHANGE_ME";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        return $"{data}|{sig}";
    }
}