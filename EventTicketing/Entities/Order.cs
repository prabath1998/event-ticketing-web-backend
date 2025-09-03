using System.ComponentModel.DataAnnotations;
using EventTicketing.Enums;

namespace EventTicketing.Entities
{
    public class Order
    {
        public long Id { get; set; }
        public long UserId { get; set; }

        [Required, MaxLength(50)]
        public string OrderNumber { get; set; } = default!;

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public int SubtotalCents { get; set; }
        public int DiscountCents { get; set; }
        public int FeesCents { get; set; }
        public int TotalCents { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; } = "LKR";

        [MaxLength(50)]
        public string? DiscountCode { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = default!;
        public Payment? Payment { get; set; }
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}