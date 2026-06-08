namespace ContentPublishing.Web.Net8.Models;

public sealed class ContentListItemViewModel
{
    public Guid ContentId { get; init; }
    public int ContentNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime LastModifiedDate { get; init; }
    public DateTime? PublishedDate { get; init; }
    public int ChapterCount { get; init; }
}