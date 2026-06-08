using ContentPublishing.Web.Net8.Data;
using ContentPublishing.Web.Net8.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContentPublishing.Web.Net8.Controllers;

[Authorize(Roles = "Reviewer")]
public class ReviewController : Controller
{
    private readonly ContentReadDbContext _db;

    public ReviewController(ContentReadDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var nextCandidates = await _db.Contents
            .AsNoTracking()
            .Where(c => c.Status == "UnderReview")
            .OrderBy(c => c.LastModifiedDate)
            .Take(10)
            .Select(c => new ContentListItemViewModel
            {
                ContentId = c.ContentId,
                ContentNumber = 0,
                Title = c.Title,
                Status = c.Status,
                LastModifiedDate = c.LastModifiedDate,
                PublishedDate = c.PublishedDate,
                ChapterCount = 0
            })
            .ToListAsync();

        var model = new ReviewerDashboardViewModel
        {
            UnderReviewCount = await _db.Contents.CountAsync(c => c.Status == "UnderReview"),
            PublishedCount = await _db.Contents.CountAsync(c => c.Status == "Published"),
            NextReviewCandidates = nextCandidates
        };

        return View(model);
    }
}