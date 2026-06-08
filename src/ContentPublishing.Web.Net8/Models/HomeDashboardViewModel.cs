namespace ContentPublishing.Web.Net8.Models;

public sealed class HomeDashboardViewModel
{
    public bool IsAuthenticated { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsReviewer { get; init; }
    public bool IsAuthor { get; init; }
}