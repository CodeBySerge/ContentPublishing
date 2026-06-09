namespace ContentPublishing.Web.Net8.Models;

public sealed class AdminContentManagementViewModel
{
    public IReadOnlyList<AdminContentItemViewModel> ReadyToPublish { get; init; } = Array.Empty<AdminContentItemViewModel>();
    public IReadOnlyList<AdminContentItemViewModel> RecentPublished { get; init; } = Array.Empty<AdminContentItemViewModel>();
}

public sealed class AdminContentItemViewModel
{
    public Guid ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime LastModifiedDate { get; init; }
    public DateTime? PublishedDate { get; init; }
}