using ContentPublishing.Domain.Entities;
using ContentPublishing.Web.Net8.Data;
using ContentPublishing.Web.Net8.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContentPublishing.Web.Net8.Controllers;

[Authorize(Roles = "Author,Admin")]
public class ChapterController : Controller
{
    private readonly ContentReadDbContext _db;

    public ChapterController(ContentReadDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid contentId)
    {
        var content = await _db.Contents.AsNoTracking().SingleOrDefaultAsync(c => c.ContentId == contentId);
        if (content == null)
        {
            return NotFound();
        }

        if (string.Equals(content.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Archived content cannot be edited.";
            return RedirectToAction("Details", "Content", new { id = contentId });
        }

        return View(new ChapterEditViewModel { ContentId = contentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChapterEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == model.ContentId);
        if (content == null)
        {
            return NotFound();
        }

        if (string.Equals(content.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Archived content cannot be edited.";
            return RedirectToAction("Details", "Content", new { id = model.ContentId });
        }

        var nextOrder = await _db.Chapters
            .Where(ch => ch.ContentId == model.ContentId && !ch.IsDeleted)
            .Select(ch => (int?)ch.ChapterOrder)
            .MaxAsync() ?? 0;

        var chapter = new Chapter
        {
            ChapterId = Guid.NewGuid(),
            ContentId = model.ContentId,
            ChapterTitle = model.ChapterTitle.Trim(),
            ChapterBody = model.ChapterBody,
            ChapterOrder = nextOrder + 1,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.Chapters.Add(chapter);
        content.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Chapter added.";
        return RedirectToAction("Details", "Content", new { id = model.ContentId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var chapter = await _db.Chapters.AsNoTracking().SingleOrDefaultAsync(ch => ch.ChapterId == id && !ch.IsDeleted);
        if (chapter == null)
        {
            return NotFound();
        }

        var content = await _db.Contents.AsNoTracking().SingleOrDefaultAsync(c => c.ContentId == chapter.ContentId);
        if (content == null)
        {
            return NotFound();
        }

        if (string.Equals(content.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Archived content cannot be edited.";
            return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
        }

        return View(new ChapterEditViewModel
        {
            ChapterId = chapter.ChapterId,
            ContentId = chapter.ContentId,
            ChapterTitle = chapter.ChapterTitle,
            ChapterBody = chapter.ChapterBody
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ChapterEditViewModel model)
    {
        if (!ModelState.IsValid || !model.ChapterId.HasValue)
        {
            return View(model);
        }

        var chapter = await _db.Chapters.SingleOrDefaultAsync(ch => ch.ChapterId == model.ChapterId.Value && !ch.IsDeleted);
        if (chapter == null)
        {
            return NotFound();
        }

        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == chapter.ContentId);
        if (content == null)
        {
            return NotFound();
        }

        if (string.Equals(content.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Archived content cannot be edited.";
            return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
        }

        chapter.ChapterTitle = model.ChapterTitle.Trim();
        chapter.ChapterBody = model.ChapterBody;
        chapter.LastModifiedDate = DateTime.UtcNow;
        content.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Chapter updated.";
        return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(Guid contentId, string? changeNotes)
    {
        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == contentId);
        if (content == null)
        {
            return NotFound();
        }

        var chapterCount = await _db.Chapters.CountAsync(ch => ch.ContentId == contentId && !ch.IsDeleted);
        if (chapterCount < 1)
        {
            TempData["ErrorMessage"] = "At least one chapter is required before submission.";
            return RedirectToAction("Details", "Content", new { id = contentId });
        }

        if (string.Equals(content.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Archived content cannot be submitted.";
            return RedirectToAction("Details", "Content", new { id = contentId });
        }

        content.Status = "UnderReview";
        content.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(changeNotes)
            ? "Draft submitted for review."
            : "Draft submitted for review with change notes.";
        return RedirectToAction("PendingReviews", "Review");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var chapter = await _db.Chapters.SingleOrDefaultAsync(ch => ch.ChapterId == id && !ch.IsDeleted);
        if (chapter == null)
        {
            return NotFound();
        }

        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == chapter.ContentId);
        if (content == null)
        {
            return NotFound();
        }

        if (string.Equals(content.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Archived content cannot be edited.";
            return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
        }

        chapter.IsDeleted = true;
        chapter.LastModifiedDate = DateTime.UtcNow;
        content.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Chapter removed.";
        return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(ReorderChaptersViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction("Details", "Content", new { id = model.ContentId });
        }

        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == model.ContentId);
        if (content == null)
        {
            return NotFound();
        }

        var ids = model.OrderedChapterIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Guid.TryParse(x.Trim(), out var parsed) ? parsed : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToList();

        var chapters = await _db.Chapters
            .Where(ch => ch.ContentId == model.ContentId && !ch.IsDeleted)
            .ToListAsync();

        if (ids.Count != chapters.Count)
        {
            TempData["ErrorMessage"] = "Invalid chapter reorder payload.";
            return RedirectToAction("Details", "Content", new { id = model.ContentId });
        }

        for (var i = 0; i < ids.Count; i++)
        {
            var chapter = chapters.SingleOrDefault(ch => ch.ChapterId == ids[i]);
            if (chapter == null)
            {
                TempData["ErrorMessage"] = "Invalid chapter list for reorder.";
                return RedirectToAction("Details", "Content", new { id = model.ContentId });
            }

            chapter.ChapterOrder = i + 1;
            chapter.LastModifiedDate = DateTime.UtcNow;
        }

        content.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Chapter order updated.";
        return RedirectToAction("Details", "Content", new { id = model.ContentId });
    }
}