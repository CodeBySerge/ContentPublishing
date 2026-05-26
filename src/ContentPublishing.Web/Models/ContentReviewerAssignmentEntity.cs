using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Models
{
    public class ContentReviewerAssignmentEntity
    {
        [Key]
        public Guid AssignmentId { get; set; }

        [Required]
        public Guid ContentId { get; set; }

        [Required]
        [StringLength(128)]
        public string ReviewerId { get; set; }

        [Required]
        [StringLength(128)]
        public string AssignedByUserId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public virtual ContentEntity Content { get; set; }
        public virtual ApplicationUser Reviewer { get; set; }
    }
}
