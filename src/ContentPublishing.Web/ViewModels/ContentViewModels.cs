using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace ContentPublishing.Web.ViewModels
{
    public class ContentEditViewModel
    {
        public Guid? ContentId { get; set; }

        [Required]
        [StringLength(250)]
        [AllowHtml]
        public string Title { get; set; }

        [AllowHtml]
        public string Description { get; set; }
    }

    public class ContentListItemViewModel
    {
        public int ContentNumber { get; set; }
        public Guid ContentId { get; set; }
        public Guid? PrimaryChapterId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int ChapterCount { get; set; }
    }

    public class ChapterListItemViewModel
    {
        public int ChapterNumber { get; set; }
        public Guid ChapterId { get; set; }
        public Guid ContentId { get; set; }
        public string ChapterTitle { get; set; }
        public int ChapterOrder { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ContentDetailsViewModel
    {
        public Guid ContentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public IList<ChapterListItemViewModel> Chapters { get; set; }
        public IList<ContentClarificationRequestItemViewModel> ClarificationRequests { get; set; }
    }

    public class ContentClarificationRequestItemViewModel
    {
        public string ReviewerName { get; set; }
        public string Message { get; set; }
        public DateTime RequestedDate { get; set; }
    }

    public class ContentVersionListItemViewModel
    {
        public Guid VersionId { get; set; }
        public int VersionNumber { get; set; }
        public string Action { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public string SnapshotJson { get; set; }
    }
}
