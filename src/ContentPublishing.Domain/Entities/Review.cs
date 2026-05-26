using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Domain.Entities
{
    public class Review
    {
        [Key]
        public Guid ReviewId { get; set; }

        public Guid ContentId { get; set; }

        public Guid ReviewerId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public string Comments { get; set; }

        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewDate { get; set; }
    }
}
