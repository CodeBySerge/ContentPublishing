using System;
using System.Collections.Generic;

namespace ContentPublishing.Web.ViewModels
{
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
        public IList<ChapterListItemViewModel> Chapters { get; set; }
    }

    public class ReviewDecisionViewModel
    {
        public Guid ContentId { get; set; }
        public Guid ReviewId { get; set; }
        public string Comments { get; set; }
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
