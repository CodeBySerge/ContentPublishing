using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContentPublishing.Web.Models
{
    public static class ContentStatuses
    {
        public const string Draft = "Draft";
        public const string UnderReview = "UnderReview";
        public const string Approved = "Approved";
        public const string Published = "Published";
        public const string Archived = "Archived";
    }

    public class ContentEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ContentNumber { get; set; }

        [Key]
        public Guid ContentId { get; set; }

        [Required]
        [StringLength(250)]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = ContentStatuses.Draft;

        [Required]
        [StringLength(128)]
        public string AuthorId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

        public DateTime? PublishedDate { get; set; }

        public DateTime? ScheduledPublishDate { get; set; }

        public DateTime? ArchivedDate { get; set; }

        public virtual ICollection<ChapterEntity> Chapters { get; set; } = new List<ChapterEntity>();
        public virtual ICollection<ContentImageEntity> Images { get; set; } = new List<ContentImageEntity>();
        public virtual ICollection<ContentVersionEntity> Versions { get; set; } = new List<ContentVersionEntity>();
    }
}
