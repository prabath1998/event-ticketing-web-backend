using System.ComponentModel.DataAnnotations;
using EventTicketing.Enums;

namespace EventTicketing.Entities
{
    public class Ticket
    {
        public long Id { get; set; }
        public long OrderItemId { get; set; }

        [Required, MaxLength(80)]
        public string TicketCode { get; set; } = default!;

        [Required, MaxLength(255)]
        public string QrPayload { get; set; } = default!;

        [MaxLength(120)]
        public string? HolderName { get; set; }

        [MaxLength(255)]
        public string? HolderEmail { get; set; }

        public TicketStatus Status { get; set; } = TicketStatus.Valid;

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CheckedInAt { get; set; }

        public OrderItem OrderItem { get; set; } = default!;
    }
}