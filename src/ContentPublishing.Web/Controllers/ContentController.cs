using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ContentPublishing.Application.Rules;
using ContentPublishing.Web.Models;
using ContentPublishing.Web.Services;
using ContentPublishing.Web.Security;
using ContentPublishing.Web.ViewModels;
using Microsoft.AspNet.Identity;

namespace ContentPublishing.Web.Controllers
{
    [Authorize(Roles = RoleNames.Author + "," + RoleNames.Administrator)]
    public class ContentController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly AuditLogService _audit;
        private readonly WorkflowNotificationService _notifications;
        private readonly ContentVersionService _versions;
        private readonly ContentImageService _images;
        private readonly PublishingService _publishing;

        public ContentController()
        {
            _audit = new AuditLogService(_db);
            _notifications = new WorkflowNotificationService(_db, new SmtpEmailService());
            _versions = new ContentVersionService(_db);
            _images = new ContentImageService(_db);
            _publishing = new PublishingService(_db);
        }

        public async Task<ActionResult> Index()
        {
            var userId = User.Identity.GetUserId();

            var contentItems = await _db.Contents
                .Where(c => c.AuthorId == userId)
                .OrderByDescending(c => c.LastModifiedDate)
                .Select(c => new ContentListItemViewModel
                {
                    ContentId = c.ContentId,
                    Title = c.Title,
                    Status = c.Status,
                    LastModifiedDate = c.LastModifiedDate,
                    ChapterCount = c.Chapters.Count(ch => !ch.IsDeleted)
                })
                .ToListAsync();

            return View(contentItems);
        }

        public ActionResult Create()
        {
            return View(new ContentEditViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ContentEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var entity = new ContentEntity
            {
                ContentId = Guid.NewGuid(),
                Title = model.Title,
                Description = model.Description,
                Status = ContentStatuses.Draft,
                AuthorId = User.Identity.GetUserId(),
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _db.Contents.Add(entity);
            await _db.SaveChangesAsync();

            if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
            {
                await _images.SavePrimaryImageAsync(entity.ContentId, model.ImageFile, model.CropX, model.CropY, model.CropWidth, model.CropHeight);
            }

            await _versions.SaveSnapshotAsync(entity.ContentId, "CREATE_CONTENT", User.Identity.GetUserId(), "Initial draft created.");

            return RedirectToAction("Details", new { id = entity.ContentId });
        }

        public async Task<ActionResult> Edit(Guid id)
        {
            var content = await FindOwnedContentAsync(id);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (!ContentWorkflowRules.CanEdit(content.Status))
            {
                TempData["ErrorMessage"] = "Archived content cannot be edited.";
                return RedirectToAction("Details", new { id });
            }

            return View(new ContentEditViewModel
            {
                ContentId = content.ContentId,
                Title = content.Title,
                Description = content.Description,
                ExistingImagePath = content.Images.OrderByDescending(i => i.CreatedDate).Where(i => i.IsPrimary).Select(i => i.RelativePath).FirstOrDefault()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
            content.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
            {
                await _images.SavePrimaryImageAsync(content.ContentId, model.ImageFile, model.CropX, model.CropY, model.CropWidth, model.CropHeight);
            }

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
                PrimaryImagePath = content.Images.OrderByDescending(i => i.CreatedDate).Where(i => i.IsPrimary).Select(i => i.RelativePath).FirstOrDefault(),
                Chapters = content.Chapters
                    .Where(ch => !ch.IsDeleted)
                    .OrderBy(ch => ch.ChapterOrder)
                    .Select(ch => new ChapterListItemViewModel
                    {
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

            return RedirectToAction("Index");
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
            var userId = User.Identity.GetUserId();
            return await _db.Contents
                .Include(c => c.Chapters)
                .Include(c => c.Images)
                .SingleOrDefaultAsync(c => c.ContentId == id && c.AuthorId == userId);
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
                reviewers = await _db.Users
                    .Where(u => u.IsActive && u.Roles.Any(role => role.RoleId == reviewerRoleId))
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName + " (" + u.Email + ")"
                    })
                    .ToListAsync();
            }

            ViewBag.ReviewerOptions = reviewers;
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
