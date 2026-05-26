using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace ContentPublishing.Web.ViewModels
{
    public class ContentEditViewModel
    {
        public Guid? ContentId { get; set; }

        [Required]
        [StringLength(250)]
        public string Title { get; set; }

        [StringLength(2000)]
        public string Description { get; set; }

        public HttpPostedFileBase ImageFile { get; set; }

        public string ExistingImagePath { get; set; }

        public int? CropX { get; set; }
        public int? CropY { get; set; }
        public int? CropWidth { get; set; }
        public int? CropHeight { get; set; }
    }

    public class ContentListItemViewModel
    {
        public Guid ContentId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int ChapterCount { get; set; }
    }

    public class ChapterListItemViewModel
    {
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
        public string PrimaryImagePath { get; set; }
        public IList<ChapterListItemViewModel> Chapters { get; set; }
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
