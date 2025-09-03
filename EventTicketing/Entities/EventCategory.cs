namespace EventTicketing.Entities
{
    public class EventCategory
    {
        public long EventId { get; set; }
        public int CategoryId { get; set; }

        public Event Event { get; set; } = default!;
        public Category Category { get; set; } = default!;
    }
}