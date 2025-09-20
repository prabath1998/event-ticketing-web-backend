using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventTicketing.Data;
using EventTicketing.Entities;
using EventTicketing.Enums;
using EventTicketing.Models.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using Discount = EventTicketing.Entities.Discount;
using StripeEvent = Stripe.Event;

namespace EventTicketing.Services.Payments
{
    public class StripeGateway : IPaymentGateway
    {
        private readonly AppDbContext _db;
        private readonly string _webhookSecret;
        private readonly string _frontendOrigin;

        public string Name => "Stripe";

        public StripeGateway(AppDbContext db, IConfiguration config, IOptions<FrontendOptions> fe)
        {
            _db = db;

            var secretKey = config["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("Stripe:SecretKey is not configured.");
            StripeConfiguration.ApiKey = secretKey;

            _webhookSecret = config["Stripe:WebhookSecret"] ?? string.Empty;
            _frontendOrigin = fe.Value.Origin?.TrimEnd('/') ?? "http://localhost:3000";
        }

        public async Task<PaymentSessionResult> CreatePaymentSessionAsync(long orderId, CancellationToken ct = default)
        {
            var order = await _db.Orders
                .Include(o => o.Items).ThenInclude(i => i.TicketType)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order is null) throw new InvalidOperationException("Order not found.");
            if (order.Status != OrderStatus.Pending) throw new InvalidOperationException("Order is not pending.");
            if (string.IsNullOrWhiteSpace(order.Currency))
                throw new InvalidOperationException("Order currency is required.");

            var currency = order.Currency.Trim().ToLowerInvariant();
           
            long SubtotalMinor = 0;

            foreach (var i in order.Items)
            {
                var unitMinor = ToMinorUnits(i.UnitPriceCents, currency);
                SubtotalMinor += unitMinor * i.Quantity;
            }
            
            long DiscountMinor = 0;
            string? normalizedCode = string.IsNullOrWhiteSpace(order.DiscountCode)
                ? null
                : order.DiscountCode.Trim().ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(normalizedCode))
            {
                var eventId = await _db.OrderItems
                    .Where(oi => oi.OrderId == order.Id)
                    .Select(oi => oi.TicketType.EventId)
                    .Distinct()
                    .SingleAsync(ct);

                var d = await _db.Discounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.EventId == eventId && x.Code == normalizedCode && x.IsActive
                                              && (x.StartsAt == null || x.StartsAt <= DateTime.UtcNow)
                                              && (x.EndsAt == null || x.EndsAt >= DateTime.UtcNow)
                                              && (x.MaxUses == null || x.UsedCount < x.MaxUses), ct);

                if (d is not null)
                {
                    DiscountMinor = ComputeDiscountMinor(d, order, currency);
                    if (DiscountMinor > SubtotalMinor) DiscountMinor = SubtotalMinor;
                }
            }

           
            long FeesMinor = (long)Math.Round(SubtotalMinor * 0.025, MidpointRounding.AwayFromZero) + 50;

           
            long TotalMinor = SubtotalMinor - DiscountMinor + FeesMinor;

          
            int minMinor = currency switch
            {
                "lkr" => 20000, 
                "usd" => 50,
                "eur" => 50,
                _ => 50
            };

            if (TotalMinor < minMinor)
            {
                
                FeesMinor += (minMinor - TotalMinor);
                TotalMinor = minMinor;
            }

           
            var lineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = TotalMinor,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Order #{order.Id}"
                        }
                    }
                }
            };

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                LineItems = lineItems,
                SuccessUrl = $"{_frontendOrigin}/payment/success",
                CancelUrl = $"{_frontendOrigin}/payment/cancel",
                ClientReferenceId = order.Id.ToString(),
                PaymentMethodTypes = new List<string> { "card" },
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["orderId"] = order.Id.ToString(),
                        ["subtotal_minor"] = SubtotalMinor.ToString(CultureInfo.InvariantCulture),
                        ["discount_minor"] = DiscountMinor.ToString(CultureInfo.InvariantCulture),
                        ["fees_minor"] = FeesMinor.ToString(CultureInfo.InvariantCulture),
                        ["total_minor"] = TotalMinor.ToString(CultureInfo.InvariantCulture),
                        ["currency"] = currency.ToUpperInvariant(),
                        ["discount_code"] = normalizedCode ?? ""
                    }
                }
            };

            Session session;
            try
            {
                var sessionService = new SessionService();
                session = await sessionService.CreateAsync(options, cancellationToken: ct);
            }
            catch (StripeException ex)
            {
                throw new InvalidOperationException($"Stripe Checkout error: {ex.Message}");
            }
            
            order.SubtotalCents = (int)SubtotalMinor;
            order.DiscountCents = (int)DiscountMinor;
            order.FeesCents = (int)FeesMinor;
            order.TotalCents = (int)TotalMinor;

            if (order.Payment == null)
            {
                order.Payment = new Payment
                {
                    OrderId = order.Id,
                    Provider = PaymentProvider.Stripe,
                    Status = PaymentStatus.Initiated,
                    AmountCents = (int)TotalMinor,
                    Currency = currency.ToUpperInvariant(),
                    ProviderSessionId = session.Id,
                    ProviderRef = session.Id,
                    PaidAt = null,
                    RawResponse = null
                };
                _db.Payments.Add(order.Payment);
            }
            else
            {
                order.Payment.Provider = PaymentProvider.Stripe;
                order.Payment.Status = PaymentStatus.Initiated; 
                order.Payment.AmountCents = (int)TotalMinor;
                order.Payment.Currency = currency.ToUpperInvariant();
                order.Payment.ProviderSessionId = session.Id;
                order.Payment.ProviderRef = session.Id;
                order.Payment.PaidAt = null;
                order.Payment.RawResponse = null;
            }

            await _db.SaveChangesAsync(ct);

            return new PaymentSessionResult(
                Provider: Name,
                ClientSecret: null,
                RedirectUrl: session.Url,
                SessionId: session.Id,
                RequiresRedirect: true
            );
        }


        public async Task<(long orderId, bool success)> HandleWebhookAsync(
            string payload, string? signature, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_webhookSecret))
                return (0, false);

            StripeEvent stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(payload, signature, _webhookSecret);
            }
            catch
            {
                return (0, false);
            }

            long orderId = 0;
            string? sessionId = null;
            string? paymentIntentId = null;

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session == null) return (0, false);
                    sessionId = session.Id;
                    paymentIntentId = session.PaymentIntentId;

                    if (!long.TryParse(session.ClientReferenceId, out orderId))
                        return (0, false);
                    break;
                }

                case "payment_intent.succeeded":
                {
                    var pi = stripeEvent.Data.Object as PaymentIntent;
                    if (pi == null) return (0, false);
                    paymentIntentId = pi.Id;

                    if (pi.Metadata != null &&
                        pi.Metadata.TryGetValue("orderId", out var s) &&
                        long.TryParse(s, out var oid))
                    {
                        orderId = oid;
                    }

                    if (orderId == 0) return (0, false);
                    break;
                }

                default:

                    return (0, true);
            }

            var order = await _db.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order is null) return (0, false);

            if (order.Payment == null)
            {
                order.Payment = new Payment
                {
                    OrderId = order.Id,
                    Provider = PaymentProvider.Stripe,
                    Status = PaymentStatus.Initiated,
                    AmountCents = order.TotalCents,
                    Currency = order.Currency
                };
                _db.Payments.Add(order.Payment);
            }

            order.Status = OrderStatus.Paid;
            order.Payment.Status = PaymentStatus.Captured;
            order.Payment.PaidAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(sessionId))
                order.Payment.ProviderSessionId = sessionId;
            if (!string.IsNullOrEmpty(paymentIntentId))
                order.Payment.ProviderRef = paymentIntentId;
            order.Payment.RawResponse = payload;

            await _db.SaveChangesAsync(ct);
            return (order.Id, true);
        }

        private static long ToMinorUnits(long major, string currency)
        {
            return currency.Equals("jpy", StringComparison.OrdinalIgnoreCase) ? major : major * 100;
        }

        private static long ComputeDiscountMinor(Discount d, Order order, string currency)
        {
            long baseMinor;

            if (d.Scope == DiscountScope.TicketType && d.TicketTypeId.HasValue)
            {
                baseMinor = 0;
                foreach (var i in order.Items.Where(x => x.TicketTypeId == d.TicketTypeId.Value))
                {
                    var unitMinor = ToMinorUnits(i.UnitPriceCents, currency);
                    baseMinor += unitMinor * i.Quantity;
                }
            }
            else
            {
                
                long subtotalMinor = 0;
                foreach (var i in order.Items)
                {
                    var unitMinor = ToMinorUnits(i.UnitPriceCents, currency);
                    subtotalMinor += unitMinor * i.Quantity;
                }

                baseMinor = subtotalMinor;
            }

            if (baseMinor <= 0) return 0;

            return d.Type switch
            {
                DiscountType.Percentage => (long)Math.Floor(baseMinor * (d.Value / 100.0)),
                DiscountType.Amount => ToMinorUnits(d.Value, currency), 
                _ => 0
            };
        }
    }
}