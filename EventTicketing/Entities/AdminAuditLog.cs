using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class AdminAuditLog
    {
        public long Id { get; set; }
        public long ActorUserId { get; set; }

        [Required, MaxLength(120)]
        public string Action { get; set; } = default!;

        [Required, MaxLength(80)]
        public string EntityType { get; set; } = default!;

        public long? EntityId { get; set; }

        public string? Meta { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User Actor { get; set; } = default!;
    }
}