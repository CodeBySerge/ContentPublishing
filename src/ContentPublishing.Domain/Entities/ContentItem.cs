using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Domain.Entities
{
    public class ContentItem
    {
        [Key]
        public Guid ContentId { get; set; }

        [Required]
        [MaxLength(250)]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Draft";

        public Guid AuthorId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

        public DateTime? PublishedDate { get; set; }

        public DateTime? ArchivedDate { get; set; }
    }
}
