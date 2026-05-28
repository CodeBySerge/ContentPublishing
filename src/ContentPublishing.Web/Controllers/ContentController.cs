using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web;
using ContentPublishing.Application.Rules;
using ContentPublishing.Web.Models;
using ContentPublishing.Web.Services;
using ContentPublishing.Web.Security;
using ContentPublishing.Web.ViewModels;
using Microsoft.AspNet.Identity;
using System.Text.RegularExpressions;

namespace ContentPublishing.Web.Controllers
{
    [Authorize(Roles = RoleNames.Author + "," + RoleNames.Administrator)]
    public class ContentController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly AuditLogService _audit;
        private readonly WorkflowNotificationService _notifications;
        private readonly ContentVersionService _versions;
        private readonly PublishingService _publishing;

        public ContentController()
        {
            _audit = new AuditLogService(_db);
            _notifications = new WorkflowNotificationService(_db, new SmtpEmailService());
            _versions = new ContentVersionService(_db);
            _publishing = new PublishingService(_db);
        }

        public async Task<ActionResult> Index()
        {
            var contentItems = (await _db.Contents
                .Select(c => new ContentListItemViewModel
                {
                    ContentNumber = c.ContentNumber,
                    ContentId = c.ContentId,
                    Title = c.Title,
                    Status = c.Status,
                    LastModifiedDate = c.LastModifiedDate,
                    ChapterCount = c.Chapters.Count(ch => !ch.IsDeleted),
                    PrimaryChapterId = c.Chapters
                        .Where(ch => !ch.IsDeleted)
                        .OrderBy(ch => ch.ChapterOrder)
                        .Select(ch => (Guid?)ch.ChapterId)
                        .FirstOrDefault()
                })
                .ToListAsync())
                .OrderBy(c => ExtractChapterNumber(c.Title))
                .ThenBy(c => c.Title)
                .ToList();

            foreach (var item in contentItems)
            {
                var chapterNumber = ExtractChapterNumber(item.Title);
                if (chapterNumber != int.MaxValue)
                {
                    item.ContentNumber = chapterNumber;
                }
            }

            return View(contentItems);
        }

        public ActionResult Create()
        {
            return Redirect("~/Content/Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Create(ContentEditViewModel model)
        {
            TempData["ErrorMessage"] = "Manual content creation is disabled. Select a handbook chapter and edit it.";
            return Redirect("~/Content/Index");
        }

        public async Task<ActionResult> Edit(Guid id)
        {
            var content = await FindOwnedContentAsync(id);
            if (content == null)
            {
                TempData["ErrorMessage"] = "Selected content was not found. Choose a handbook chapter from Edit Content.";
                return Redirect("~/Content/Index");
            }

            if (!ContentWorkflowRules.CanEdit(content.Status))
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", new { id });
            }

            var primaryChapter = content.Chapters
                .Where(ch => !ch.IsDeleted)
                .OrderBy(ch => ch.ChapterOrder)
                .Select(ch => (Guid?)ch.ChapterId)
                .FirstOrDefault();

            if (primaryChapter.HasValue)
            {
                return RedirectToAction("Edit", "Chapter", new { id = primaryChapter.Value });
            }

            return View(new ContentEditViewModel
            {
                ContentId = content.ContentId,
                Title = content.Title,
                Description = BuildEditableDraftBody(content)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public async Task<ActionResult> Edit(ContentEditViewModel model)
        {
            if (!ModelState.IsValid || !model.ContentId.HasValue)
            {
                return View(model);
            }

            var content = await FindOwnedContentAsync(model.ContentId.Value);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (!ContentWorkflowRules.CanEdit(content.Status))
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", new { id = model.ContentId.Value });
            }

            content.Title = model.Title;
            content.Description = model.Description;
            SyncCombinedChaptersForDraft(content, model.Title, model.Description, ensureChapter: content.Chapters.Any(ch => !ch.IsDeleted));
            content.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _versions.SaveSnapshotAsync(content.ContentId, "EDIT_CONTENT", User.Identity.GetUserId(), "Content metadata updated.");

            return RedirectToAction("Details", new { id = content.ContentId });
        }

        public async Task<ActionResult> Details(Guid id)
        {
            var content = await FindOwnedContentAsync(id);
            if (content == null)
            {
                return HttpNotFound();
            }

            await PopulateReviewerSelectionAsync();

            var model = new ContentDetailsViewModel
            {
                ContentId = content.ContentId,
                Title = content.Title,
                Description = content.Description,
                Status = content.Status,
                CreatedDate = content.CreatedDate,
                LastModifiedDate = content.LastModifiedDate,
                Chapters = content.Chapters
                    .Where(ch => !ch.IsDeleted)
                    .OrderBy(ch => ch.ChapterOrder)
                    .Select(ch => new ChapterListItemViewModel
                    {
                        ChapterNumber = ch.ChapterNumber,
                        ChapterId = ch.ChapterId,
                        ContentId = ch.ContentId,
                        ChapterTitle = ch.ChapterTitle,
                        ChapterOrder = ch.ChapterOrder,
                        IsDeleted = ch.IsDeleted
                    })
                    .ToList()
            };

            return View(model);
        }

        public async Task<ActionResult> Delete(Guid id)
        {
            var content = await FindOwnedContentAsync(id);
            if (content == null)
            {
                return HttpNotFound();
            }

            var model = new ContentListItemViewModel
            {
                ContentId = content.ContentId,
                Title = content.Title,
                Status = content.Status,
                LastModifiedDate = content.LastModifiedDate,
                ChapterCount = content.Chapters.Count(ch => !ch.IsDeleted)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<ActionResult> DeleteConfirmed(Guid id)
        {
            var content = await FindOwnedContentAsync(id);
            if (content == null)
            {
                return HttpNotFound();
            }

            var previousStatus = content.Status;
            content.Status = ContentStatuses.Archived;
            content.ArchivedDate = DateTime.UtcNow;
            content.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                userId: User.Identity.GetUserId(),
                action: AuditActions.StatusChange,
                entityType: "Content",
                entityId: content.ContentId,
                oldValue: previousStatus,
                newValue: content.Status,
                ipAddress: Request.UserHostAddress,
                changeDetails: "Content archived by author/admin.");

            await _versions.SaveSnapshotAsync(content.ContentId, "ARCHIVE_CONTENT", User.Identity.GetUserId(), "Content archived.");

            return Redirect("~/Content/Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Submit(Guid id, string reviewerIds)
        {
            var content = await FindOwnedContentAsync(id);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (!ContentWorkflowRules.CanEdit(content.Status))
            {
                TempData["ErrorMessage"] = "Archived content cannot be submitted.";
                return RedirectToAction("Details", new { id });
            }

            var chapterCount = content.Chapters.Count(ch => !ch.IsDeleted);
            if (!ContentWorkflowRules.CanSubmitForReview(content.Status, chapterCount))
            {
                TempData["ErrorMessage"] = "At least one chapter is required before submission.";
                return RedirectToAction("Details", new { id });
            }

            var previousStatus = content.Status;
            content.Status = ContentStatuses.UnderReview;
            content.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var parsedReviewerIds = ParseReviewerIds(reviewerIds);
            if (parsedReviewerIds.Any())
            {
                foreach (var reviewerId in parsedReviewerIds)
                {
                    var alreadyAssigned = await _db.ContentReviewerAssignments.AnyAsync(a => a.ContentId == content.ContentId && a.ReviewerId == reviewerId && a.IsActive);
                    if (!alreadyAssigned)
                    {
                        _db.ContentReviewerAssignments.Add(new ContentReviewerAssignmentEntity
                        {
                            AssignmentId = Guid.NewGuid(),
                            ContentId = content.ContentId,
                            ReviewerId = reviewerId,
                            AssignedByUserId = User.Identity.GetUserId(),
                            AssignedDate = DateTime.UtcNow,
                            IsActive = true
                        });
                    }

                    var hasReview = await _db.Reviews.AnyAsync(r => r.ContentId == content.ContentId && r.ReviewerId == reviewerId && r.Status == ReviewStatuses.Pending);
                    if (!hasReview)
                    {
                        _db.Reviews.Add(new ReviewEntity
                        {
                            ReviewId = Guid.NewGuid(),
                            ContentId = content.ContentId,
                            ReviewerId = reviewerId,
                            Status = ReviewStatuses.Pending,
                            SubmittedDate = DateTime.UtcNow
                        });
                    }
                }

                await _db.SaveChangesAsync();
            }

            await _audit.LogAsync(
                userId: User.Identity.GetUserId(),
                action: AuditActions.StatusChange,
                entityType: "Content",
                entityId: content.ContentId,
                oldValue: previousStatus,
                newValue: content.Status,
                ipAddress: Request.UserHostAddress,
                changeDetails: "Content submitted for review.");

            if (parsedReviewerIds.Any())
            {
                await _notifications.NotifyContentSubmittedAsync(content, parsedReviewerIds);
            }

            await _versions.SaveSnapshotAsync(content.ContentId, "SUBMIT_CONTENT", User.Identity.GetUserId(), parsedReviewerIds.Any() ? "Submitted with selected reviewers." : "Submitted without selected reviewers.");

            TempData["SuccessMessage"] = parsedReviewerIds.Any()
                ? "Content submitted for review and reviewers assigned."
                : "Content submitted for review. Admin can assign reviewers.";
            return RedirectToAction("Details", new { id });
        }

        public async Task<ActionResult> VersionHistory(Guid id)
        {
            var content = await FindOwnedContentAsync(id);
            if (content == null)
            {
                return HttpNotFound();
            }

            var versions = await _db.ContentVersions
                .Where(v => v.ContentId == id)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();

            ViewBag.ContentId = id;
            return View(versions);
        }

        [AllowAnonymous]
        public async Task<ActionResult> Published()
        {
            await _publishing.PublishDueContentAsync(Request?.UserHostAddress);

            var publishedItems = await _db.Contents
                .Where(c => c.Status == ContentStatuses.Published)
                .OrderByDescending(c => c.PublishedDate)
                .Select(c => new ContentListItemViewModel
                {
                    ContentId = c.ContentId,
                    Title = c.Title,
                    Status = c.Status,
                    LastModifiedDate = c.LastModifiedDate,
                    ChapterCount = c.Chapters.Count(ch => !ch.IsDeleted)
                })
                .ToListAsync();

            return View(publishedItems);
        }

        private async Task<ContentEntity> FindOwnedContentAsync(Guid id)
        {
            return await _db.Contents
                .Include(c => c.Chapters)
                .Include(c => c.Images)
                .SingleOrDefaultAsync(c => c.ContentId == id);
        }

        private async Task PopulateReviewerSelectionAsync()
        {
            var reviewerRoleId = await _db.Roles
                .Where(r => r.Name == RoleNames.Reviewer)
                .Select(r => r.Id)
                .SingleOrDefaultAsync();

            var reviewers = new List<SelectListItem>();
            if (!string.IsNullOrWhiteSpace(reviewerRoleId))
            {
                var reviewerUsers = await _db.Users
                    .Where(u => u.IsActive && u.EmailConfirmed && u.Roles.Any(role => role.RoleId == reviewerRoleId))
                    .OrderBy(u => u.FullName)
                    .ToListAsync();

                reviewers = reviewerUsers
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName + " (" + u.Email + ")"
                    })
                    .ToList();
            }

            ViewBag.ReviewerOptions = reviewers;
        }

        private void SyncCombinedChaptersForDraft(ContentEntity content, string title, string combinedBody, bool ensureChapter)
        {
            var activeChapters = content.Chapters
                .Where(ch => !ch.IsDeleted)
                .OrderBy(ch => ch.ChapterOrder)
                .ToList();

            var shouldHaveChapter = ensureChapter || activeChapters.Any() || !string.IsNullOrWhiteSpace(combinedBody);
            if (!shouldHaveChapter)
            {
                return;
            }

            var chapterTitle = BuildCombinedChapterTitle(title);
            var chapterBody = combinedBody ?? string.Empty;
            var now = DateTime.UtcNow;

            if (!activeChapters.Any())
            {
                _db.Chapters.Add(new ChapterEntity
                {
                    ChapterId = Guid.NewGuid(),
                    ContentId = content.ContentId,
                    ChapterTitle = chapterTitle,
                    ChapterBody = chapterBody,
                    ChapterOrder = 1,
                    CreatedDate = now,
                    LastModifiedDate = now,
                    IsDeleted = false
                });

                return;
            }

            var primaryChapter = activeChapters[0];
            primaryChapter.ChapterTitle = chapterTitle;
            primaryChapter.ChapterBody = chapterBody;
            primaryChapter.ChapterOrder = 1;
            primaryChapter.LastModifiedDate = now;
            primaryChapter.IsDeleted = false;

            foreach (var chapter in activeChapters.Skip(1))
            {
                chapter.IsDeleted = true;
                chapter.LastModifiedDate = now;
            }
        }

        private static string BuildEditableDraftBody(ContentEntity content)
        {
            var activeChapters = content.Chapters
                .Where(ch => !ch.IsDeleted)
                .OrderBy(ch => ch.ChapterOrder)
                .ToList();

            if (!activeChapters.Any())
            {
                return content.Description;
            }

            if (activeChapters.Count == 1)
            {
                var chapterBody = activeChapters[0].ChapterBody ?? string.Empty;
                var description = content.Description ?? string.Empty;
                if (description.Trim() == chapterBody.Trim())
                {
                    return content.Description;
                }
            }

            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(content.Description))
            {
                parts.Add(content.Description);
            }

            foreach (var chapter in activeChapters)
            {
                var chapterBody = chapter.ChapterBody ?? string.Empty;
                var heading = "<h3>" + HttpUtility.HtmlEncode(chapter.ChapterTitle ?? string.Empty) + "</h3>";
                parts.Add(heading + chapterBody);
            }

            return parts.Any() ? string.Join("<hr />", parts) : content.Description;
        }

        private static string BuildCombinedChapterTitle(string title)
        {
            var plainTitle = HttpUtility.HtmlDecode(title ?? string.Empty);
            plainTitle = System.Text.RegularExpressions.Regex.Replace(plainTitle, "<.*?>", string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(plainTitle))
            {
                return "Combined Draft";
            }

            return plainTitle.Length > 250 ? plainTitle.Substring(0, 250) : plainTitle;
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

        private static int ExtractChapterNumber(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return int.MaxValue;
            }

            var match = Regex.Match(title, "\\b(\\d+)\\b");
            if (!match.Success)
            {
                return int.MaxValue;
            }

            return int.TryParse(match.Groups[1].Value, out var value) ? value : int.MaxValue;
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
