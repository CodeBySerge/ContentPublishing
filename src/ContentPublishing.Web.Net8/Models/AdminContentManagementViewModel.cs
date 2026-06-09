namespace ContentPublishing.Web.Net8.Models;

public sealed class AdminContentManagementViewModel
{
    public IReadOnlyList<AdminContentItemViewModel> ReadyToPublish { get; init; } = Array.Empty<AdminContentItemViewModel>();
    public IReadOnlyList<AdminContentItemViewModel> RecentPublished { get; init; } = Array.Empty<AdminContentItemViewModel>();
    public IReadOnlyList<AdminPendingWorkItemViewModel> PendingWorkItems { get; init; } = Array.Empty<AdminPendingWorkItemViewModel>();
    public IReadOnlyList<AdminReviewerOptionViewModel> ReviewerOptions { get; init; } = Array.Empty<AdminReviewerOptionViewModel>();
}

public sealed class AdminContentItemViewModel
{
    public Guid ContentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime LastModifiedDate { get; init; }
    public DateTime? PublishedDate { get; init; }
}

public sealed class AdminPendingWorkItemViewModel
{
    public Guid ReviewId { get; init; }
    public Guid ContentId { get; init; }
    public string ContentTitle { get; init; } = string.Empty;
    public string ReviewerId { get; init; } = string.Empty;
    public string ReviewerName { get; init; } = string.Empty;
    public DateTime SubmittedDate { get; init; }
}

public sealed class AdminReviewerOptionViewModel
{
    public string ReviewerId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}