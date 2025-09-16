using System.ComponentModel.DataAnnotations;
using EventTicketing.Enums;

namespace EventTicketing.Entities
{
    public class Event
    {
        public long Id { get; set; }
        public long OrganizerId { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; } = default!;

        public string? Description { get; set; }

        [Required, MaxLength(255)]
        public string VenueName { get; set; } = default!;

        [MaxLength(120)]
        public string? LocationCity { get; set; }

        [MaxLength(255)]
        public string? LocationAddress { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public EventStatus Status { get; set; } = EventStatus.Draft;

        [MaxLength(500)]
         // Image properties
        public byte[]? ImageData { get; set; }
        public string? ImageContentType { get; set; }
        public string? ImageFileName { get; set; }
        public long? ImageFileSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public OrganizerProfile Organizer { get; set; } = default!;
        public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
        public ICollection<EventCategory> EventCategories { get; set; } = new List<EventCategory>();
    }
}