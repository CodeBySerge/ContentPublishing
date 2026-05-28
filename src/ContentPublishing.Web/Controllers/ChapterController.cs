using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using ContentPublishing.Web.Models;
using ContentPublishing.Web.Services;
using ContentPublishing.Web.Security;
using ContentPublishing.Web.ViewModels;
using Microsoft.AspNet.Identity;

namespace ContentPublishing.Web.Controllers
{
    [Authorize(Roles = RoleNames.Author + "," + RoleNames.Administrator)]
    public class ChapterController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly ContentVersionService _versions;

        public ChapterController()
        {
            _versions = new ContentVersionService(_db);
        }

        public async Task<ActionResult> Create(Guid contentId)
        {
            var content = await FindOwnedContentAsync(contentId);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (content.Status == ContentStatuses.Archived)
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", "Content", new { id = contentId });
            }

            return View(new ChapterEditViewModel
            {
                ContentId = contentId,
                ContentNumber = content.ContentNumber
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public async Task<ActionResult> Create(ChapterEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var content = await FindOwnedContentAsync(model.ContentId);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (content.Status == ContentStatuses.Archived)
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", "Content", new { id = model.ContentId });
            }

            var nextOrder = content.Chapters.Where(ch => !ch.IsDeleted).Select(ch => (int?)ch.ChapterOrder).Max() ?? 0;
            var entity = new ChapterEntity
            {
                ChapterId = Guid.NewGuid(),
                ContentId = model.ContentId,
                ChapterTitle = model.ChapterTitle,
                ChapterBody = model.ChapterBody,
                ChapterOrder = nextOrder + 1,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.Chapters.Add(entity);
            content.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _versions.SaveSnapshotAsync(model.ContentId, "CREATE_CHAPTER", User.Identity.GetUserId(), "Chapter added.");

            return RedirectToAction("Details", "Content", new { id = model.ContentId });
        }

        public async Task<ActionResult> Edit(Guid id)
        {
            var chapter = await FindOwnedChapterAsync(id);
            if (chapter == null || chapter.IsDeleted)
            {
                return HttpNotFound();
            }

            if (chapter.Content.Status == ContentStatuses.Archived)
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
            }

            return View(new ChapterEditViewModel
            {
                ChapterNumber = chapter.ChapterNumber,
                ContentNumber = chapter.Content.ContentNumber,
                ChapterId = chapter.ChapterId,
                ContentId = chapter.ContentId,
                ChapterTitle = chapter.ChapterTitle,
                ChapterBody = chapter.ChapterBody
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public async Task<ActionResult> Edit(ChapterEditViewModel model)
        {
            if (!ModelState.IsValid || !model.ChapterId.HasValue)
            {
                return View(model);
            }

            var chapter = await FindOwnedChapterAsync(model.ChapterId.Value);
            if (chapter == null || chapter.IsDeleted)
            {
                return HttpNotFound();
            }

            if (chapter.Content.Status == ContentStatuses.Archived)
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
            }

            chapter.ChapterTitle = model.ChapterTitle;
            chapter.ChapterBody = model.ChapterBody;
            chapter.LastModifiedDate = DateTime.UtcNow;
            chapter.Content.LastModifiedDate = DateTime.UtcNow;
            chapter.Content.Status = ContentStatuses.Draft;
            chapter.Content.PublishedDate = null;
            chapter.Content.ScheduledPublishDate = null;
            await _db.SaveChangesAsync();
            await _versions.SaveSnapshotAsync(chapter.ContentId, "EDIT_CHAPTER", User.Identity.GetUserId(), "Chapter updated.");

            TempData["SuccessMessage"] = "Chapter saved as draft. Submit for approval when ready.";
            return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(Guid id)
        {
            var chapter = await FindOwnedChapterAsync(id);
            if (chapter == null || chapter.IsDeleted)
            {
                return HttpNotFound();
            }

            if (chapter.Content.Status == ContentStatuses.Archived)
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
            }

            chapter.IsDeleted = true;
            chapter.LastModifiedDate = DateTime.UtcNow;
            chapter.Content.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _versions.SaveSnapshotAsync(chapter.ContentId, "DELETE_CHAPTER", User.Identity.GetUserId(), "Chapter soft-deleted.");

            return RedirectToAction("Details", "Content", new { id = chapter.ContentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Reorder(ReorderChaptersViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Details", "Content", new { id = model.ContentId });
            }

            var content = await FindOwnedContentAsync(model.ContentId);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (content.Status == ContentStatuses.Archived)
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", "Content", new { id = model.ContentId });
            }

            var ids = new List<Guid>();
            foreach (var part in model.OrderedChapterIds.Split(',').Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                if (Guid.TryParse(part.Trim(), out var parsedId))
                {
                    ids.Add(parsedId);
                }
            }

            var activeChapters = content.Chapters.Where(ch => !ch.IsDeleted).ToList();
            if (ids.Count != activeChapters.Count)
            {
                TempData["ErrorMessage"] = "Invalid chapter reorder payload.";
                return RedirectToAction("Details", "Content", new { id = model.ContentId });
            }

            for (var i = 0; i < ids.Count; i++)
            {
                var chapter = activeChapters.SingleOrDefault(ch => ch.ChapterId == ids[i]);
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
            await _versions.SaveSnapshotAsync(content.ContentId, "REORDER_CHAPTERS", User.Identity.GetUserId(), "Chapter order changed.");

            TempData["SuccessMessage"] = "Chapter order updated.";
            return RedirectToAction("Details", "Content", new { id = model.ContentId });
        }

        private async Task<ContentEntity> FindOwnedContentAsync(Guid contentId)
        {
            return await _db.Contents
                .Include(c => c.Chapters)
                .SingleOrDefaultAsync(c => c.ContentId == contentId);
        }

        private async Task<ChapterEntity> FindOwnedChapterAsync(Guid chapterId)
        {
            return await _db.Chapters
                .Include(ch => ch.Content)
                .SingleOrDefaultAsync(ch => ch.ChapterId == chapterId);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
