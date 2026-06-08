namespace ContentPublishing.Web.Net8.Models;

public sealed class PendingReviewItemViewModel
{
    public Guid ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime LastModifiedDate { get; init; }
    public int ChapterCount { get; init; }
}

public sealed class ReviewContentViewModel
{
    public Guid ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ContentStatus { get; init; } = string.Empty;
    public DateTime LastModifiedDate { get; init; }
    public IReadOnlyList<ReviewChapterItemViewModel> Chapters { get; init; } = Array.Empty<ReviewChapterItemViewModel>();
}

public sealed class ReviewChapterItemViewModel
{
    public Guid ChapterId { get; init; }
    public int ChapterOrder { get; init; }
    public string ChapterTitle { get; init; } = string.Empty;
    public string ChapterBody { get; init; } = string.Empty;
}