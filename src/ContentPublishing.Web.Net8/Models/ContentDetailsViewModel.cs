namespace ContentPublishing.Web.Net8.Models;

public sealed class ContentDetailsViewModel
{
    public Guid ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public DateTime LastModifiedDate { get; init; }
    public IReadOnlyList<ContentClarificationRequestItemViewModel> ClarificationRequests { get; init; } = Array.Empty<ContentClarificationRequestItemViewModel>();
    public IReadOnlyList<ChapterItemViewModel> Chapters { get; init; } = Array.Empty<ChapterItemViewModel>();
}

public sealed class ContentClarificationRequestItemViewModel
{
    public string ReviewerName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime RequestedDate { get; init; }
}

public sealed class ChapterItemViewModel
{
    public Guid ChapterId { get; init; }
    public int ChapterOrder { get; init; }
    public string ChapterTitle { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
}