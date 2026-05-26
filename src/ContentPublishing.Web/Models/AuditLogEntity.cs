using System;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Models
{
    public static class AuditActions
    {
        public const string Create = "CREATE";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string Approve = "APPROVE";
        public const string Reject = "REJECT";
        public const string Publish = "PUBLISH";
        public const string SchedulePublish = "SCHEDULE_PUBLISH";
        public const string Submit = "SUBMIT";
        public const string AssignReviewer = "ASSIGN_REVIEWER";
        public const string StatusChange = "STATUS_CHANGE";
    }

    public class AuditLogEntity
    {
        [Key]
        public Guid LogId { get; set; }

        [StringLength(128)]
        public string UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        [Required]
        [StringLength(50)]
        public string EntityType { get; set; }

        public Guid EntityId { get; set; }

        public string OldValue { get; set; }

        public string NewValue { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string IpAddress { get; set; }

        public string ChangeDetails { get; set; }
    }
}
