using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class TicketType
    {
        public long Id { get; set; }
        public long EventId { get; set; }

        [Required, MaxLength(120)]
        public string Name { get; set; } = default!;

        [MaxLength(255)]
        public string? Description { get; set; }

        public int PriceCents { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; } = "LKR";

        public int TotalQuantity { get; set; }
        public int SoldQuantity { get; set; } = 0;

        public DateTime SalesStart { get; set; }
        public DateTime SalesEnd { get; set; }

        public int? PerOrderLimit { get; set; } = 10;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Event Event { get; set; } = default!;
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<Discount> Discounts { get; set; } = new List<Discount>();
    }
}