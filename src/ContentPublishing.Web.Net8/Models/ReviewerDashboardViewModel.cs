namespace ContentPublishing.Web.Net8.Models;

public sealed class ReviewerDashboardViewModel
{
    public int UnderReviewCount { get; init; }
    public int PublishedCount { get; init; }
    public IReadOnlyList<ContentListItemViewModel> NextReviewCandidates { get; init; } = Array.Empty<ContentListItemViewModel>();
}