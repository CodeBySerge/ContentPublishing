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
        private readonly WorkflowNotificationService _notifications;

        public ChapterController()
        {
            _versions = new ContentVersionService(_db);
            _notifications = new WorkflowNotificationService(_db, new SmtpEmailService());
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
            var safeTitle = HtmlContentSanitizer.StripScripts(model.ChapterTitle);
            var safeBody = HtmlContentSanitizer.StripScripts(model.ChapterBody);
            var entity = new ChapterEntity
            {
                ChapterId = Guid.NewGuid(),
                ContentId = model.ContentId,
                ChapterTitle = safeTitle,
                ChapterBody = safeBody,
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

            var selectedReviewerIds = await _db.ContentReviewerAssignments
                .Where(a => a.ContentId == chapter.ContentId && a.IsActive)
                .Select(a => a.ReviewerId)
                .ToListAsync();

            await PopulateReviewerSelectionAsync(selectedReviewerIds);

            return View(new ChapterEditViewModel
            {
                ChapterNumber = chapter.ChapterNumber,
                ContentNumber = chapter.Content.ContentNumber,
                ChapterId = chapter.ChapterId,
                ContentId = chapter.ContentId,
                ChapterTitle = chapter.ChapterTitle,
                ChapterBody = chapter.ChapterBody,
                ReviewerIds = string.Join(",", selectedReviewerIds)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public async Task<ActionResult> Edit(ChapterEditViewModel model)
        {
            if (!ModelState.IsValid || !model.ChapterId.HasValue)
            {
                await PopulateReviewerSelectionAsync(ParseReviewerIds(model.ReviewerIds));
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

            var requestedReviewerIds = ParseReviewerIds(model.ReviewerIds);
            if (!requestedReviewerIds.Any())
            {
                ModelState.AddModelError("ReviewerIds", "Select at least one reviewer before saving the draft.");
                await PopulateReviewerSelectionAsync(requestedReviewerIds);
                return View(model);
            }

            var eligibleReviewerIds = await GetEligibleReviewerIdsAsync(requestedReviewerIds);
            if (eligibleReviewerIds.Count != requestedReviewerIds.Count)
            {
                ModelState.AddModelError("ReviewerIds", "Invalid reviewer selection. Use the dropdown and choose active Admin or Reviewer accounts.");
                await PopulateReviewerSelectionAsync(requestedReviewerIds);
                return View(model);
            }

            var safeChangeNotes = HtmlContentSanitizer.StripScripts(model.ChangeNotes);

            chapter.ChapterTitle = HtmlContentSanitizer.StripScripts(model.ChapterTitle);
            chapter.ChapterBody = HtmlContentSanitizer.StripScripts(model.ChapterBody);
            chapter.LastModifiedDate = DateTime.UtcNow;
            chapter.Content.LastModifiedDate = DateTime.UtcNow;
            chapter.Content.Status = ContentStatuses.UnderReview;
            chapter.Content.PublishedDate = null;
            chapter.Content.ScheduledPublishDate = null;

            var existingAssignments = await _db.ContentReviewerAssignments
                .Where(a => a.ContentId == chapter.ContentId)
                .ToListAsync();

            foreach (var assignment in existingAssignments)
            {
                assignment.IsActive = eligibleReviewerIds.Contains(assignment.ReviewerId, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var reviewerId in eligibleReviewerIds)
            {
                var assignment = existingAssignments.FirstOrDefault(a => a.ReviewerId == reviewerId);
                if (assignment == null)
                {
                    _db.ContentReviewerAssignments.Add(new ContentReviewerAssignmentEntity
                    {
                        AssignmentId = Guid.NewGuid(),
                        ContentId = chapter.ContentId,
                        ReviewerId = reviewerId,
                        AssignedByUserId = User.Identity.GetUserId(),
                        AssignedDate = DateTime.UtcNow,
                        IsActive = true
                    });
                }
                else
                {
                    assignment.IsActive = true;
                    assignment.AssignedByUserId = User.Identity.GetUserId();
                    assignment.AssignedDate = DateTime.UtcNow;
                }

                var hasPending = await _db.Reviews.AnyAsync(r => r.ContentId == chapter.ContentId && r.ReviewerId == reviewerId && r.Status == ReviewStatuses.Pending);
                if (!hasPending)
                {
                    _db.Reviews.Add(new ReviewEntity
                    {
                        ReviewId = Guid.NewGuid(),
                        ContentId = chapter.ContentId,
                        ReviewerId = reviewerId,
                        Status = ReviewStatuses.Pending,
                        SubmittedDate = DateTime.UtcNow,
                        AuthorChangeNotes = safeChangeNotes
                    });
                }
            }

            await _db.SaveChangesAsync();
            await _versions.SaveSnapshotAsync(chapter.ContentId, "EDIT_CHAPTER", User.Identity.GetUserId(), string.IsNullOrWhiteSpace(safeChangeNotes) ? "Chapter updated and submitted for review." : "Chapter updated with notes: " + safeChangeNotes);
            var emailSkipped = false;
            try
            {
                await _notifications.NotifyContentSubmittedAsync(chapter.Content, eligibleReviewerIds, safeChangeNotes);
            }
            catch
            {
                // Email is optional in this flow; assignments and review records are already persisted.
                emailSkipped = true;
            }

            TempData["SuccessMessage"] = emailSkipped
                ? "Draft saved and routed to selected reviewers. Email notification was skipped."
                : "Draft saved and routed to selected reviewers.";
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

        private async Task PopulateReviewerSelectionAsync(IEnumerable<string> selectedReviewerIds)
        {
            var selected = (selectedReviewerIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var eligibleUsers = await GetEligibleReviewerQuery()
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.ReviewerOptions = eligibleUsers
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName + " (" + u.Email + ")",
                    Selected = selected.Contains(u.Id, StringComparer.OrdinalIgnoreCase)
                })
                .ToList();
        }

        private IQueryable<ApplicationUser> GetEligibleReviewerQuery()
        {
            var eligibleRoleIds = _db.Roles
                .Where(r => r.Name == RoleNames.Reviewer || r.Name == RoleNames.Administrator)
                .Select(r => r.Id);

            return _db.Users
                .Where(u => eligibleRoleIds.Contains(u.RoleId) || u.Roles.Any(ur => eligibleRoleIds.Contains(ur.RoleId)));
        }

        private async Task<List<string>> GetEligibleReviewerIdsAsync(IEnumerable<string> requestedReviewerIds)
        {
            var requested = requestedReviewerIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!requested.Any())
            {
                return new List<string>();
            }

            return await GetEligibleReviewerQuery()
                .Where(u => requested.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();
        }

        private static List<string> ParseReviewerIds(string reviewerIds)
        {
            if (string.IsNullOrWhiteSpace(reviewerIds))
            {
                return new List<string>();
            }

            return reviewerIds
                .Split(',')
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
