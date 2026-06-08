using ContentPublishing.Infrastructure.Data;
using ContentPublishing.Web.Net8.Models;
using Microsoft.AspNetCore.Mvc;

namespace ContentPublishing.Web.Net8.Controllers;

public class ContentController : Controller
{
    private readonly IConfiguration _configuration;

    public ContentController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var connectionString = _configuration.GetConnectionString("ContentPublishingDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            TempData["ErrorMessage"] = "Missing ContentPublishingDb connection string in appsettings.";
            return View(new List<ContentListItemViewModel>());
        }

        try
        {
            using var db = new ContentPublishingDbContext(connectionString);

            var chapterLookup = db.Chapters
                .Where(ch => !ch.IsDeleted)
                .GroupBy(ch => ch.ContentId)
                .ToDictionary(g => g.Key, g => g.Count());

            var items = db.Contents
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
                .ToList();

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
    public IActionResult Details(Guid id)
    {
        var connectionString = _configuration.GetConnectionString("ContentPublishingDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            TempData["ErrorMessage"] = "Missing ContentPublishingDb connection string in appsettings.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var db = new ContentPublishingDbContext(connectionString);
            var content = db.Contents.SingleOrDefault(c => c.ContentId == id);
            if (content == null)
            {
                return NotFound();
            }

            var chapters = db.Chapters
                .Where(ch => ch.ContentId == id && !ch.IsDeleted)
                .OrderBy(ch => ch.ChapterOrder)
                .Select(ch => new ChapterItemViewModel
                {
                    ChapterId = ch.ChapterId,
                    ChapterOrder = ch.ChapterOrder,
                    ChapterTitle = ch.ChapterTitle,
                    IsDeleted = ch.IsDeleted
                })
                .ToList();

            var model = new ContentDetailsViewModel
            {
                ContentId = content.ContentId,
                Title = content.Title,
                Description = content.Description ?? string.Empty,
                Status = content.Status,
                CreatedDate = content.CreatedDate,
                LastModifiedDate = content.LastModifiedDate,
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
    public IActionResult Published()
    {
        var connectionString = _configuration.GetConnectionString("ContentPublishingDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            TempData["ErrorMessage"] = "Missing ContentPublishingDb connection string in appsettings.";
            return View(new List<ContentListItemViewModel>());
        }

        try
        {
            using var db = new ContentPublishingDbContext(connectionString);

            var chapterLookup = db.Chapters
                .Where(ch => !ch.IsDeleted)
                .GroupBy(ch => ch.ContentId)
                .ToDictionary(g => g.Key, g => g.Count());

            var items = db.Contents
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
                .ToList();

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