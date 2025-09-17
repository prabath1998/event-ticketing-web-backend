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
    private readonly IConfiguration _config;


    public PaymentsController(AppDbContext db, IPaymentGateway gateway, ITicketService tickets,IConfiguration config)
    {
        _db = db; _gateway = gateway; _tickets = tickets; _config = config;
    }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    // PaymentsController.cs
    [HttpPost("{orderId:long}/initiate")]
    [Authorize]
    public async Task<IActionResult> Initiate(long orderId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var order = await _db.Orders
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);

        if (order is null) return NotFound();
        if (order.Status != OrderStatus.Pending) return BadRequest("Order is not pending.");

        if (order.Payment == null)
        {
            order.Payment = new Payment
            {
                OrderId = order.Id,
                Provider = PaymentProvider.Dummy,
                Status = PaymentStatus.Initiated,
                AmountCents = order.TotalCents,
                Currency = order.Currency
            };
            _db.Payments.Add(order.Payment);
            await _db.SaveChangesAsync(ct);
        }
       
        var useDummy = _config.GetValue<bool>("Payments:UseDummy", true);
        if (useDummy)
        {
            return Ok(new PaymentInitResponseDto(
                Provider: "dummy",
                OrderId: order.Id,
                RequiresRedirect: false
            ));
        }
        
        var session = await _gateway.CreatePaymentSessionAsync(order.Id, ct);
        return Ok(new PaymentInitResponseDto(
            Provider: _gateway.Name,
            OrderId: order.Id,
            RequiresRedirect: true
        ));
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
          
            await _tickets.IssueForPaidOrderAsync(order.Id, ct);
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
    
     [HttpPost("{orderId:long}/dummy-confirm")]
    [Authorize]
    public async Task<IActionResult> DummyConfirm(long orderId, CancellationToken ct)
    {
        var allowDummy = _config.GetValue<bool>("Payments:AllowDummyConfirm", true);
        if (!allowDummy)
            return BadRequest("Dummy confirm is disabled.");

        var order = await _db.Orders
            .Include(o => o.Payment)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null) return NotFound("Order not found.");
       
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && order.UserId != userId) return Forbid();
       
        if (order.Status == OrderStatus.Paid)
        {
            await _tickets.IssueForPaidOrderAsync(order.Id, ct); 
            return Ok(new { order.Id, order.Status, message = "Order already paid; tickets ensured." });
        }

        if (order.Status != OrderStatus.Pending)
            return BadRequest($"Order is not pending. Current status: {order.Status}");

        
        if (order.Payment == null)
        {
            order.Payment = new Payment
            {
                OrderId = order.Id,
                Provider = PaymentProvider.Dummy,   
                Status = PaymentStatus.Captured,
                AmountCents = order.TotalCents,
                Currency = order.Currency,
                PaidAt = DateTime.UtcNow,
                RawResponse = "{ \"dummy\": true }"
            };
            _db.Payments.Add(order.Payment);
        }
        else
        {
            order.Payment.Status = PaymentStatus.Captured;
            order.Payment.PaidAt = DateTime.UtcNow;
            order.Payment.RawResponse = "{ \"dummy\": true }";
            order.Payment.Provider = PaymentProvider.Dummy; 
        }

        order.Status = OrderStatus.Paid;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        
        await _tickets.IssueForPaidOrderAsync(order.Id, ct);

        return Ok(new { order.Id, order.Status });
    }
}
