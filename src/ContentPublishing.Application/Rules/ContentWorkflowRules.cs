using System;

namespace ContentPublishing.Application.Rules
{
    public static class ContentWorkflowRules
    {
        public static bool CanEdit(string status)
        {
            return !string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanSubmitForReview(string status, int activeChapterCount)
        {
            return CanEdit(status) && activeChapterCount >= 1;
        }

        public static bool CanApprove(string status)
        {
            return string.Equals(status, "UnderReview", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanPublish(string status)
        {
            return string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldAutoPublish(string status, DateTime? scheduledPublishDateUtc, DateTime nowUtc)
        {
            return CanPublish(status) && scheduledPublishDateUtc.HasValue && scheduledPublishDateUtc.Value <= nowUtc;
        }

        public static string ResolveStatusAfterApproval(string currentStatus, int pendingReviewCountAfterDecision)
        {
            return pendingReviewCountAfterDecision <= 0 ? "Approved" : currentStatus;
        }

        public static string ResolveStatusAfterRejection()
        {
            return "Draft";
        }
    }
}
