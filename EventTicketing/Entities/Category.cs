using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class Category
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = default!;

        [Required, MaxLength(120)]
        public string Slug { get; set; } = default!;

        public ICollection<EventCategory> EventCategories { get; set; } = new List<EventCategory>();
    }
}