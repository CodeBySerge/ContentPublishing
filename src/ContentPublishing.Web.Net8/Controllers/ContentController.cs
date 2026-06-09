using ContentPublishing.Web.Net8.Data;
using ContentPublishing.Web.Net8.Models;
using ContentPublishing.Web.Net8.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContentPublishing.Web.Net8.Controllers;

[Authorize(Roles = "Author,Reviewer,Admin")]
public class ContentController : Controller
{
    private const string ClarificationPrefix = "CLARIFICATION_REQUEST::";
    private readonly ContentReadDbContext _db;
    private readonly AppIdentityDbContext _identityDb;

    public ContentController(ContentReadDbContext db, AppIdentityDbContext identityDb)
    {
        _db = db;
        _identityDb = identityDb;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var chapterLookup = await _db.Chapters
                .AsNoTracking()
                .Where(ch => !ch.IsDeleted)
                .GroupBy(ch => ch.ContentId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var items = await _db.Contents
                .AsNoTracking()
                .OrderBy(c => c.Title)
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

            var normalized = items
                .Select(i => new ContentListItemViewModel
                {
                    ContentId = i.ContentId,
                    ContentNumber = ExtractChapterNumber(i.Title),
                    Title = i.Title,
                    Status = i.Status,
                    LastModifiedDate = i.LastModifiedDate,
                    PublishedDate = i.PublishedDate,
                    ChapterCount = chapterLookup.TryGetValue(i.ContentId, out var count) ? count : 0
                })
                .OrderBy(i => i.ContentNumber)
                .ThenBy(i => i.Title)
                .ToList();

            return View(normalized);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Unable to load content: {ex.Message}";
            return View(new List<ContentListItemViewModel>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var content = await _db.Contents
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.ContentId == id);
            if (content == null)
            {
                return NotFound();
            }

            var chapters = await _db.Chapters
                .AsNoTracking()
                .Where(ch => ch.ContentId == id && !ch.IsDeleted)
                .OrderBy(ch => ch.ChapterOrder)
                .Select(ch => new ChapterItemViewModel
                {
                    ChapterId = ch.ChapterId,
                    ChapterOrder = ch.ChapterOrder,
                    ChapterTitle = ch.ChapterTitle,
                    IsDeleted = ch.IsDeleted
                })
                .ToListAsync();

            var clarificationReviews = await _db.Reviews
                .AsNoTracking()
                .Where(r => r.ContentId == id && r.Status == "Pending" && r.Comments != null && r.Comments.StartsWith(ClarificationPrefix))
                .OrderByDescending(r => r.SubmittedDate)
                .ToListAsync();

            var reviewerIds = clarificationReviews
                .Select(r => r.ReviewerId)
                .Distinct()
                .ToList();

            var reviewerLookup = reviewerIds.Count == 0
                ? new Dictionary<string, string>()
                : await _identityDb.Users
                    .AsNoTracking()
                    .Where(u => reviewerIds.Contains(u.Id))
                    .ToDictionaryAsync(
                        u => u.Id,
                        u => !string.IsNullOrWhiteSpace(u.Email)
                            ? u.Email
                            : (u.UserName ?? "Reviewer"));

            var model = new ContentDetailsViewModel
            {
                ContentId = content.ContentId,
                Title = content.Title,
                Description = content.Description ?? string.Empty,
                Status = content.Status,
                CreatedDate = content.CreatedDate,
                LastModifiedDate = content.LastModifiedDate,
                ClarificationRequests = clarificationReviews
                    .Select(r => new ContentClarificationRequestItemViewModel
                    {
                        ReviewerName = reviewerLookup.TryGetValue(r.ReviewerId, out var name) ? name : "Reviewer",
                        Message = (r.Comments ?? string.Empty).Substring(ClarificationPrefix.Length),
                        RequestedDate = r.SubmittedDate
                    })
                    .ToList(),
                Chapters = chapters
            };

            return View(model);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Unable to load content details: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Published()
    {
        try
        {
            var chapterLookup = await _db.Chapters
                .AsNoTracking()
                .Where(ch => !ch.IsDeleted)
                .GroupBy(ch => ch.ContentId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var items = await _db.Contents
                .AsNoTracking()
                .Where(c => c.Status == "Published")
                .OrderByDescending(c => c.PublishedDate)
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

            var normalized = items
                .Select(i => new ContentListItemViewModel
                {
                    ContentId = i.ContentId,
                    ContentNumber = ExtractChapterNumber(i.Title),
                    Title = i.Title,
                    Status = i.Status,
                    LastModifiedDate = i.LastModifiedDate,
                    PublishedDate = i.PublishedDate,
                    ChapterCount = chapterLookup.TryGetValue(i.ContentId, out var count) ? count : 0
                })
                .ToList();

            return View(normalized);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Unable to load published content: {ex.Message}";
            return View(new List<ContentListItemViewModel>());
        }
    }

    private static int ExtractChapterNumber(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return int.MaxValue;
        }

        var digits = new string(title.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var parsed) ? parsed : int.MaxValue;
    }
}