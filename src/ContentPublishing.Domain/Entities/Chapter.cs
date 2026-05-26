using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Domain.Entities
{
    public class Chapter
    {
        [Key]
        public Guid ChapterId { get; set; }

        public Guid ContentId { get; set; }

        [Required]
        [MaxLength(250)]
        public string ChapterTitle { get; set; }

        [Required]
        public string ChapterBody { get; set; }

        public int ChapterOrder { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }
    }
}
