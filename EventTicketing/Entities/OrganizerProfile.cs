using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class OrganizerProfile
    {
        public long Id { get; set; }
        public long UserId { get; set; }

        [Required, MaxLength(255)]
        public string CompanyName { get; set; } = default!;

        [MaxLength(100)]
        public string? BRN { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        public User User { get; set; } = default!;
        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}