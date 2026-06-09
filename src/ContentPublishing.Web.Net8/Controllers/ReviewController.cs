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

    [HttpGet]
    public async Task<IActionResult> PendingReviews()
    {
        var chapterLookup = await _db.Chapters
            .AsNoTracking()
            .Where(ch => !ch.IsDeleted)
            .GroupBy(ch => ch.ContentId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        var items = await _db.Contents
            .AsNoTracking()
            .Where(c => c.Status == "UnderReview")
            .OrderBy(c => c.LastModifiedDate)
            .Select(c => new PendingReviewItemViewModel
            {
                ContentId = c.ContentId,
                Title = c.Title,
                Status = c.Status,
                LastModifiedDate = c.LastModifiedDate,
                ChapterCount = 0
            })
            .ToListAsync();

        var model = items
            .Select(i => new PendingReviewItemViewModel
            {
                ContentId = i.ContentId,
                Title = i.Title,
                Status = i.Status,
                LastModifiedDate = i.LastModifiedDate,
                ChapterCount = chapterLookup.TryGetValue(i.ContentId, out var count) ? count : 0
            })
            .ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ReviewContent(Guid contentId)
    {
        var content = await _db.Contents
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.ContentId == contentId);

        if (content == null)
        {
            return NotFound();
        }

        var chapters = await _db.Chapters
            .AsNoTracking()
            .Where(ch => ch.ContentId == contentId && !ch.IsDeleted)
            .OrderBy(ch => ch.ChapterOrder)
            .Select(ch => new ReviewChapterItemViewModel
            {
                ChapterId = ch.ChapterId,
                ChapterOrder = ch.ChapterOrder,
                ChapterTitle = ch.ChapterTitle,
                ChapterBody = ch.ChapterBody
            })
            .ToListAsync();

        var model = new ReviewContentViewModel
        {
            ContentId = content.ContentId,
            Title = content.Title,
            Description = content.Description ?? string.Empty,
            ContentStatus = content.Status,
            LastModifiedDate = content.LastModifiedDate,
            Chapters = chapters
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ReviewDecisionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid review request.";
            return RedirectToAction(nameof(PendingReviews));
        }

        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == model.ContentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(PendingReviews));
        }

        if (!string.Equals(content.Status, "UnderReview", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only UnderReview content can be approved.";
            return RedirectToAction(nameof(ReviewContent), new { contentId = model.ContentId });
        }

        content.Status = "Approved";
        content.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Content approved and moved to Admin publishing queue.";
        return RedirectToAction(nameof(PendingReviews));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ReviewDecisionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid review request.";
            return RedirectToAction(nameof(PendingReviews));
        }

        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == model.ContentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(PendingReviews));
        }

        if (!string.Equals(content.Status, "UnderReview", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only UnderReview content can be rejected.";
            return RedirectToAction(nameof(ReviewContent), new { contentId = model.ContentId });
        }

        content.Status = "Draft";
        content.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Content rejected and returned to Draft.";
        return RedirectToAction(nameof(PendingReviews));
    }
}