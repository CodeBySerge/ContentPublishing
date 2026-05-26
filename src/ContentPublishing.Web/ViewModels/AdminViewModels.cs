using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalContent { get; set; }
        public int UnderReviewCount { get; set; }
        public int ApprovedCount { get; set; }
        public int PublishedCount { get; set; }
    }

    public class AdminContentListItemViewModel
    {
        public Guid ContentId { get; set; }
        public string Title { get; set; }
        public string AuthorName { get; set; }
        public string Status { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int AssignedReviewerCount { get; set; }
        public DateTime? ScheduledPublishDate { get; set; }
    }

    public class AssignReviewerViewModel
    {
        [Required]
        public Guid ContentId { get; set; }

        [Required]
        public string ReviewerId { get; set; }
    }

    public class AuditLogListItemViewModel
    {
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public Guid EntityId { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string IpAddress { get; set; }
        public string ChangeDetails { get; set; }
    }

    public class UserListItemViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public IList<string> Roles { get; set; }
    }
}
