using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Models
{
    public static class ReviewStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public class ReviewEntity
    {
        [Key]
        public Guid ReviewId { get; set; }

        [Required]
        public Guid ContentId { get; set; }

        [Required]
        [StringLength(128)]
        public string ReviewerId { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = ReviewStatuses.Pending;

        public string Comments { get; set; }

        [StringLength(2000)]
        public string AuthorChangeNotes { get; set; }

        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewDate { get; set; }

        public virtual ContentEntity Content { get; set; }
        public virtual ApplicationUser Reviewer { get; set; }
    }
}
