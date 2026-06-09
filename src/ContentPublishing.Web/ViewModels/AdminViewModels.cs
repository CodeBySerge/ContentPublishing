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

    public class AdminContentQueueViewModel
    {
        public IList<AdminQueueItemViewModel> PendingItems { get; set; }
        public IList<AdminQueueItemViewModel> AwaitingPreviewItems { get; set; }
        public IList<AdminQueueItemViewModel> CompletedPreviewItems { get; set; }
    }

    public class AdminQueueItemViewModel
    {
        public Guid ContentId { get; set; }
        public string Title { get; set; }
        public string AuthorName { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int ChapterCount { get; set; }
        public string QueueStatusLabel { get; set; }
    }

    public class AdminContentPreviewViewModel
    {
        public Guid ContentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string AuthorName { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string QueueStatusLabel { get; set; }
        public bool CanApproveForQueue { get; set; }
        public bool CanMarkAsReady { get; set; }
        public bool CanPublish { get; set; }
        public IList<AdminPreviewChapterItemViewModel> Chapters { get; set; }
    }

    public class AdminPreviewChapterItemViewModel
    {
        public string ChapterTitle { get; set; }
        public string ChapterBody { get; set; }
        public int ChapterOrder { get; set; }
    }

    public class AdminHandbookPreviewViewModel
    {
        public Guid FocusContentId { get; set; }
        public string FocusTitle { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
        public int TotalSectionCount { get; set; }
        public int TotalChapterCount { get; set; }
        public IList<AdminHandbookSectionViewModel> Sections { get; set; }
    }

    public class AdminHandbookSectionViewModel
    {
        public Guid ContentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int ChapterCount { get; set; }
        public IList<AdminPreviewChapterItemViewModel> Chapters { get; set; }
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
        public string Description { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public IList<string> Roles { get; set; }
        public string AssignedRoleName { get; set; }
        public string AssignedRoleDescription { get; set; }
    }

    public class ReviewerMetricListItemViewModel
    {
        public string ReviewerId { get; set; }
        public string ReviewerName { get; set; }
        public string ReviewerEmail { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int AverageTurnaroundHours { get; set; }
        public int ApprovalRate { get; set; }
    }

    public class ReviewerMetricsViewModel
    {
        public DateTime GeneratedAtUtc { get; set; }
        public IList<ReviewerMetricListItemViewModel> Reviewers { get; set; }
    }
}
