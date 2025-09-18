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
using EventTicketing.Services.Audit;

namespace EventTicketing.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ITicketService _tickets;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;


    public PaymentsController(AppDbContext db, IPaymentGateway gateway, ITicketService tickets, IConfiguration config,
        IAuditService audit)
    {
        _db = db;
        _gateway = gateway;
        _tickets = tickets;
        _config = config;
        _audit = audit;
    }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }


    [HttpPost("{orderId:long}/initiate")]
    [Authorize]
    public async Task<PaymentSessionResult> InitiatePaymentAsync(long orderId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = await _db.Orders
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null)
            throw new InvalidOperationException("Order not found");

        if (order.Status == OrderStatus.Paid)
        {
            await tx.CommitAsync(ct);
            return new PaymentSessionResult(
                Provider: "internal",
                ClientSecret: null,
                RedirectUrl: null,
                SessionId: null,
                RequiresRedirect: false
            );
        }

        var amountCents = order.TotalCents;

        var payment = order.Payment;
        if (payment is null)
        {
            payment = new Payment
            {
                OrderId = order.Id,
                AmountCents = amountCents,
                Currency = order.Currency,
                Status = PaymentStatus.Initiated,
                Provider = PaymentProvider.Stripe,
                PaidAt = null,
                ProviderRef = null,
                ProviderSessionId = null,
                RawResponse = null
            };

            _db.Payments.Add(payment);
            await _tickets.IssueForPaidOrderAsync(order.Id, ct);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            if (payment.Status == PaymentStatus.Captured || payment.Status == PaymentStatus.Succeeded)
            {
                await tx.CommitAsync(ct);
                return new PaymentSessionResult(
                    Provider: "internal",
                    ClientSecret: null,
                    RedirectUrl: null,
                    SessionId: null,
                    RequiresRedirect: false
                );
            }

            payment.AmountCents = amountCents;
            payment.Currency = order.Currency;
            payment.Status = PaymentStatus.Initiated;
            payment.PaidAt = null;
            payment.Provider = PaymentProvider.Stripe;
            payment.ProviderRef = null;
            payment.ProviderSessionId = null;
            payment.RawResponse = null;

            _db.Payments.Update(payment);
            await _db.SaveChangesAsync(ct);
        }


        var session = await _gateway.CreatePaymentSessionAsync(order.Id, ct);


        payment.ProviderSessionId = session.SessionId;
        payment.ProviderRef = session.SessionId;
        _db.Payments.Update(payment);
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);


        return new PaymentSessionResult(
            Provider: session.Provider,
            ClientSecret: session.ClientSecret,
            RedirectUrl: session.RedirectUrl,
            SessionId: session.SessionId,
            RequiresRedirect: session.RequiresRedirect
        );
    }

    [HttpPost("webhook/stripe")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        Request.EnableBuffering();

        string json;
        using (var reader = new StreamReader(Request.Body, leaveOpen: true))
            json = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["Stripe-Signature"].ToString();

        var (orderId, ok) = await _gateway.HandleWebhookAsync(json, signature, ct);


        if (ok && orderId == 0) return Ok();
        if (!ok) return BadRequest();

        var order = await _db.Orders
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null) return NotFound();


        try
        {
            await _tickets.IssueForPaidOrderAsync(order.Id, ct);
        }
        catch (Exception)
        {
        }

        return Ok();
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
                Provider = PaymentProvider.Stripe,
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
            order.Payment.Provider = PaymentProvider.Stripe;
        }

        order.Status = OrderStatus.Paid;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _tickets.IssueForPaidOrderAsync(order.Id, ct);

        return Ok(new { order.Id, order.Status });
    }
}