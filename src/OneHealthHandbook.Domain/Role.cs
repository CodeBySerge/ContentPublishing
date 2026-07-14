using System.ComponentModel.DataAnnotations;

namespace OneHealthHandbook.Domain.Entities
{
    public class Role
    {
        [Key]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string RoleName { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }
    }
}

