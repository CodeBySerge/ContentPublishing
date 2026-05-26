using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Models
{
    public class ContentVersionEntity
    {
        [Key]
        public Guid VersionId { get; set; }

        [Required]
        public Guid ContentId { get; set; }

        public int VersionNumber { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; }

        [StringLength(128)]
        public string CreatedByUserId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public string SnapshotJson { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }

        public virtual ContentEntity Content { get; set; }
    }
}
