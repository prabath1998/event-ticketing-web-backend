using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Entities
{
    public class Role
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = default!;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}