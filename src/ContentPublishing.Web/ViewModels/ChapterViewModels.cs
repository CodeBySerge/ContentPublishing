using System;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace ContentPublishing.Web.ViewModels
{
    public class ChapterEditViewModel
    {
        public Guid? ChapterId { get; set; }

        [Required]
        public Guid ContentId { get; set; }

        [Required]
        [StringLength(250)]
        public string ChapterTitle { get; set; }

        [Required]
        [AllowHtml]
        public string ChapterBody { get; set; }
    }

    public class ReorderChaptersViewModel
    {
        [Required]
        public Guid ContentId { get; set; }

        [Required]
        public string OrderedChapterIds { get; set; }
    }
}
