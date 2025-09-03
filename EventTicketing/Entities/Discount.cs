using System.ComponentModel.DataAnnotations;
using EventTicketing.Enums;

namespace EventTicketing.Entities
{
    public class Discount
    {
        public long Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = default!;

        public DiscountType Type { get; set; }
        public int Value { get; set; }

        public DiscountScope Scope { get; set; } = DiscountScope.Order;

        public long? TicketTypeId { get; set; }

        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public int? MaxUses { get; set; }
        public int UsedCount { get; set; } = 0;
        public int? MinSubtotalCents { get; set; }

        public bool IsActive { get; set; } = true;

        public TicketType? TicketType { get; set; }
    }
}