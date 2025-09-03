using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class LoyaltyLedger
    {
        public long Id { get; set; }
        public long UserId { get; set; }

        public int DeltaPoints { get; set; }

        [MaxLength(255)]
        public string? Reason { get; set; }

        public long? OrderId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = default!;
        public Order? Order { get; set; }
    }
}