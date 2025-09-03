using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class User
    {
        public long Id { get; set; }

        [Required, MaxLength(255)]
        public string Email { get; set; } = default!;

        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = default!;

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = default!;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = default!;

        [MaxLength(50)]
        public string? Phone { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public OrganizerProfile? OrganizerProfile { get; set; }
        public CustomerProfile? CustomerProfile { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<AdminAuditLog> AuditLogs { get; set; } = new List<AdminAuditLog>();
        public ICollection<LoyaltyLedger> LoyaltyLedger { get; set; } = new List<LoyaltyLedger>();
    }
}