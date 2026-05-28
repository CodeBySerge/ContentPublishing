using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContentPublishing.Web.Models
{
    public class ChapterEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ChapterNumber { get; set; }

        [Key]
        public Guid ChapterId { get; set; }

        [Required]
        public Guid ContentId { get; set; }

        [Required]
        [StringLength(250)]
        public string ChapterTitle { get; set; }

        [Required]
        public string ChapterBody { get; set; }

        public int ChapterOrder { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }

        public virtual ContentEntity Content { get; set; }
    }
}
