using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Domain.Entities
{
    public class ContentReviewerAssignment
    {
        [Key]
        public Guid AssignmentId { get; set; }

        public Guid ContentId { get; set; }

        public Guid ReviewerId { get; set; }

        public Guid AssignedByUserId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }
}
