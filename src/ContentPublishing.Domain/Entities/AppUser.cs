using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Domain.Entities
{
    public class AppUser
    {
        [Key]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; }

        [Required]
        [MaxLength(256)]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public int RoleId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastLogin { get; set; }

        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
    }
}
