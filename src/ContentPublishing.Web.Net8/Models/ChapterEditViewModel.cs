using System.ComponentModel.DataAnnotations;

namespace ContentPublishing.Web.Net8.Models;

public sealed class ChapterEditViewModel
{
    public Guid ContentId { get; set; }
    public Guid? ChapterId { get; set; }

    [Required]
    [StringLength(250)]
    public string ChapterTitle { get; set; } = string.Empty;

    [Required]
    public string ChapterBody { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? ChangeNotes { get; set; }
}

public sealed class ReorderChaptersViewModel
{
    [Required]
    public Guid ContentId { get; set; }

    [Required]
    public string OrderedChapterIds { get; set; } = string.Empty;
}