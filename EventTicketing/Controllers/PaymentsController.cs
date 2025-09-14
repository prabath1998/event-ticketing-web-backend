using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Entities;
using EventTicketing.Enums;
using EventTicketing.Services.Payments;
using EventTicketing.Services.Tickets;
using System.Security.Claims;

namespace EventTicketing.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ITicketService _tickets;

    public PaymentsController(AppDbContext db, IPaymentGateway gateway, ITicketService tickets)
    {
        _db = db; _gateway = gateway; _tickets = tickets;
    }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    [HttpPost("{orderId:long}/initiate")]
    [Authorize] 
    public async Task<IActionResult> Initiate(long orderId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var order = await _db.Orders.Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);

        if (order == null) return NotFound();
        if (order.Status != OrderStatus.Pending) return BadRequest("Order is not pending.");
       
        if (order.Payment == null)
        {
            order.Payment = new Payment
            {
                OrderId = order.Id,
                Provider = PaymentProvider.Other, 
                Status = PaymentStatus.Initiated,
                AmountCents = order.TotalCents,
                Currency = order.Currency
            };
            _db.Payments.Add(order.Payment);
            await _db.SaveChangesAsync(ct);
        }

        var res = await _gateway.CreatePaymentAsync(order, ct);
        return Ok(new PaymentInitResponseDto(res.Provider, res.ClientSecret, res.RedirectUrl));
    }
  
    [HttpPost("webhook/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(string provider, [FromBody] PaymentWebhookDto dto, CancellationToken ct)
    {
        if (!string.Equals(provider, _gateway.Name, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Unknown provider.");

        var (orderId, success) = await _gateway.HandleWebhookAsync(dto.Payload, dto.Signature, ct);
        if (orderId == 0) return BadRequest("Invalid payload.");

        var order = await _db.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null) return NotFound();

        if (success)
        {
            order.Status = OrderStatus.Paid;
            if (order.Payment != null)
            {
                order.Payment.Status = PaymentStatus.Captured;
                order.Payment.PaidAt = DateTime.UtcNow;
                order.Payment.RawResponse = dto.Payload;
            }
            await _db.SaveChangesAsync(ct);
          
            await _tickets.IssueForOrderAsync(order.Id, ct);
        }
        else
        {
            order.Status = OrderStatus.Failed;
            if (order.Payment != null)
            {
                order.Payment.Status = PaymentStatus.Failed;
                order.Payment.RawResponse = dto.Payload;
            }
            await _db.SaveChangesAsync(ct);
           
        }

        return Ok(new { order.Id, order.Status });
    }
}
