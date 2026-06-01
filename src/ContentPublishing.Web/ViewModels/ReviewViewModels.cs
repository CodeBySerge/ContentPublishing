using System;
using System.Collections.Generic;

namespace ContentPublishing.Web.ViewModels
{
    public class ReviewDashboardViewModel
    {
        public int PendingReviewCount { get; set; }
        public int CompletedReviewCount { get; set; }
        public int NotificationCount { get; set; }
        public DateTime? OldestPendingSubmitted { get; set; }
        public IList<PendingReviewListItemViewModel> NextPendingReviews { get; set; }
    }

    public class ReviewNotificationListItemViewModel
    {
        public Guid ContentId { get; set; }
        public Guid? ReviewId { get; set; }
        public string NotificationType { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ReviewNotificationsViewModel
    {
        public int TotalNotifications { get; set; }
        public IList<ReviewNotificationListItemViewModel> Items { get; set; }
    }

    public class PendingReviewListItemViewModel
    {
        public Guid ContentId { get; set; }
        public Guid ReviewId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public DateTime SubmittedDate { get; set; }
        public int ChapterCount { get; set; }
    }

    public class ReviewContentViewModel
    {
        public Guid ContentId { get; set; }
        public Guid ReviewId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ContentStatus { get; set; }
        public string ExistingComments { get; set; }
        public string AuthorChangeNotes { get; set; }
        public string HighlightedChangesHtml { get; set; }
        public string HighlightedChangesIsolatedHtml { get; set; }
        public string HighlightedChangesFullHtml { get; set; }
        public bool HasHighlightedChanges { get; set; }
        public string FullPreviewHtml { get; set; }
        public IList<ChapterListItemViewModel> Chapters { get; set; }
    }

    public class ReviewDecisionViewModel
    {
        public Guid ContentId { get; set; }
        public Guid ReviewId { get; set; }
        public string Comments { get; set; }
    }

    public class ReviewClarificationRequestViewModel
    {
        public Guid ContentId { get; set; }
        public Guid ReviewId { get; set; }
        public string Message { get; set; }
    }

    public class ReviewHistoryItemViewModel
    {
        public Guid ReviewId { get; set; }
        public Guid ContentId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string Comments { get; set; }
        public DateTime SubmittedDate { get; set; }
        public DateTime? ReviewDate { get; set; }
    }
}
