using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class RefreshToken
    {
        public long Id { get; set; }
        public long UserId { get; set; }

        [Required, MaxLength(500)]
        public string Token { get; set; } = default!;

        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = default!;
    }
}