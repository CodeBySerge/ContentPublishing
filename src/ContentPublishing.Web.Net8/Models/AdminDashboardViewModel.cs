namespace ContentPublishing.Web.Net8.Models;

public sealed class AdminDashboardViewModel
{
    public int TotalContent { get; init; }
    public int DraftCount { get; init; }
    public int UnderReviewCount { get; init; }
    public int ApprovedCount { get; init; }
    public int PublishedCount { get; init; }
}