using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class CustomerProfile
    {
        public long Id { get; set; }
        public long UserId { get; set; }

        public DateTime? Dob { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        public int LoyaltyPoints { get; set; } = 0;

        public User User { get; set; } = default!;
    }
}