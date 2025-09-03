namespace EventTicketing.Entities
{
    public class OrderItem
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
        public long EventId { get; set; }
        public long TicketTypeId { get; set; }

        public int UnitPriceCents { get; set; }
        public int Quantity { get; set; }
        public int LineTotalCents { get; set; }

        public string? SnapshotJson { get; set; }

        public Order Order { get; set; } = default!;
        public Event Event { get; set; } = default!;
        public TicketType TicketType { get; set; } = default!;
        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}