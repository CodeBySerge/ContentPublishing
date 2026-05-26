using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Models
{
    public class ContentImageEntity
    {
        [Key]
        public Guid ImageId { get; set; }

        [Required]
        public Guid ContentId { get; set; }

        [Required]
        [StringLength(260)]
        public string FileName { get; set; }

        [Required]
        [StringLength(500)]
        public string RelativePath { get; set; }

        [StringLength(100)]
        public string ContentType { get; set; }

        public int? CropX { get; set; }
        public int? CropY { get; set; }
        public int? CropWidth { get; set; }
        public int? CropHeight { get; set; }

        public bool IsPrimary { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public virtual ContentEntity Content { get; set; }
    }
}
