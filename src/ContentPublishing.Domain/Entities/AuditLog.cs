using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Domain.Entities
{
    public class AuditLog
    {
        [Key]
        public Guid LogId { get; set; }

        public Guid? UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; }

        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; }

        public Guid EntityId { get; set; }

        public string OldValue { get; set; }

        public string NewValue { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string IpAddress { get; set; }

        public string ChangeDetails { get; set; }
    }
}
