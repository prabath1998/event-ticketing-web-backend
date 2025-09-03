using System.ComponentModel.DataAnnotations;
using EventTicketing.Entities;
using EventTicketing.Enums;

namespace EventTicketing.Entities
{
    public class Payment
    {
        public long Id { get; set; }
        public long OrderId { get; set; }

        public PaymentProvider Provider { get; set; }

        [MaxLength(120)]
        public string? ProviderRef { get; set; }

        public PaymentStatus Status { get; set; }

        public int AmountCents { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; } = "LKR";

        public DateTime? PaidAt { get; set; }

        public string? RawResponse { get; set; }

        public Order Order { get; set; } = default!;
    }
}