using System.Web.Mvc;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using ContentPublishing.Web.Models;
using ContentPublishing.Application.Rules;
using ContentPublishing.Web.Services;
using ContentPublishing.Web.Security;
using ContentPublishing.Web.ViewModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;

namespace ContentPublishing.Web.Controllers
{
    [Authorize(Roles = RoleNames.Administrator)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly AuditLogService _audit;
        private readonly WorkflowNotificationService _notifications;
        private readonly ContentVersionService _versions;
        private readonly PublishingService _publishing;

        public AdminController()
        {
            _audit = new AuditLogService(_db);
            _notifications = new WorkflowNotificationService(_db, new SmtpEmailService());
            _versions = new ContentVersionService(_db);
            _publishing = new PublishingService(_db);
        }

        public async Task<ActionResult> Dashboard()
        {
            await _publishing.PublishDueContentAsync(Request?.UserHostAddress);

            var model = new AdminDashboardViewModel
            {
                TotalUsers = await _db.Users.CountAsync(),
                TotalContent = await _db.Contents.CountAsync(),
                UnderReviewCount = await _db.Contents.CountAsync(c => c.Status == ContentStatuses.UnderReview),
                ApprovedCount = await _db.Contents.CountAsync(c => c.Status == ContentStatuses.Approved),
                PublishedCount = await _db.Contents.CountAsync(c => c.Status == ContentStatuses.Published)
            };

            return View(model);
        }

        public async Task<ActionResult> Users()
        {
            var roles = await _db.Roles.ToDictionaryAsync(r => r.Id, r => r.Name);
            var users = await _db.Users
                .OrderBy(u => u.FullName)
                .Select(u => new UserListItemViewModel
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    IsActive = u.IsActive,
                    Roles = u.Roles.Select(r => r.RoleId).ToList()
                })
                .ToListAsync();

            foreach (var user in users)
            {
                user.Roles = user.Roles.Select(roleId => roles.ContainsKey(roleId) ? roles[roleId] : "Unknown").ToList();
            }

            return View(users);
        }

        public ActionResult CreateUser() => RedirectToAction("Users");
        public ActionResult EditUser() => RedirectToAction("Users");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AssignRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = "User and role are required.";
                return RedirectToAction("Users");
            }

            var role = await _db.Roles.SingleOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Selected role does not exist.";
                return RedirectToAction("Users");
            }

            var userManager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            var existingRoles = await userManager.GetRolesAsync(userId);
            foreach (var existing in existingRoles)
            {
                await userManager.RemoveFromRoleAsync(userId, existing);
            }

            await userManager.AddToRoleAsync(userId, roleName);
            await _audit.LogAsync(User.Identity.GetUserId(), AuditActions.Update, "User", Guid.Empty, string.Join(",", existingRoles), roleName, Request.UserHostAddress, "Admin changed user role.");

            return RedirectToAction("Users");
        }

        public async Task<ActionResult> AuditLog()
        {
            var logs = await _db.AuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(500)
                .Select(l => new AuditLogListItemViewModel
                {
                    Timestamp = l.Timestamp,
                    UserId = l.UserId,
                    Action = l.Action,
                    EntityType = l.EntityType,
                    EntityId = l.EntityId,
                    OldValue = l.OldValue,
                    NewValue = l.NewValue,
                    IpAddress = l.IpAddress,
                    ChangeDetails = l.ChangeDetails
                })
                .ToListAsync();

            return View(logs);
        }

        public async Task<ActionResult> ContentManagement()
        {
            await _publishing.PublishDueContentAsync(Request?.UserHostAddress);
            await PopulateReviewerOptionsAsync();

            var contentItems = await _db.Contents
                .OrderByDescending(c => c.LastModifiedDate)
                .Select(c => new AdminContentListItemViewModel
                {
                    ContentId = c.ContentId,
                    Title = c.Title,
                    AuthorName = c.AuthorId,
                    Status = c.Status,
                    LastModifiedDate = c.LastModifiedDate,
                    AssignedReviewerCount = _db.ContentReviewerAssignments.Count(a => a.ContentId == c.ContentId && a.IsActive),
                    ScheduledPublishDate = c.ScheduledPublishDate
                })
                .ToListAsync();

            return View(contentItems);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Publish(Guid id, string scheduledPublishDateUtc)
        {
            var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == id);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (!ContentWorkflowRules.CanPublish(content.Status))
            {
                TempData["ErrorMessage"] = "Only approved content can be published.";
                return RedirectToAction("ContentManagement");
            }

            DateTime scheduledUtc = DateTime.MinValue;
            var hasSchedule = !string.IsNullOrWhiteSpace(scheduledPublishDateUtc) && DateTime.TryParse(scheduledPublishDateUtc, out scheduledUtc);
            var previousStatus = content.Status;
            if (hasSchedule && scheduledUtc > DateTime.UtcNow)
            {
                content.ScheduledPublishDate = DateTime.SpecifyKind(scheduledUtc, DateTimeKind.Local).ToUniversalTime();
                content.LastModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                await _audit.LogAsync(User.Identity.GetUserId(), AuditActions.SchedulePublish, "Content", content.ContentId, null, content.ScheduledPublishDate.Value.ToString("u"), Request.UserHostAddress, "Admin scheduled content publishing.");
                await _versions.SaveSnapshotAsync(content.ContentId, "SCHEDULE_PUBLISH_CONTENT", User.Identity.GetUserId(), "Content scheduled for future publishing.");

                TempData["SuccessMessage"] = "Content scheduled for publishing.";
                return RedirectToAction("ContentManagement");
            }

            content.Status = ContentStatuses.Published;
            content.PublishedDate = DateTime.UtcNow;
            content.ScheduledPublishDate = null;
            content.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(User.Identity.GetUserId(), AuditActions.Publish, "Content", content.ContentId, previousStatus, ContentStatuses.Published, Request.UserHostAddress, "Admin published approved content.");
            await _notifications.NotifyContentPublishedAsync(content);
            await _versions.SaveSnapshotAsync(content.ContentId, "PUBLISH_CONTENT", User.Identity.GetUserId(), "Content published by administrator.");

            TempData["SuccessMessage"] = "Content published successfully.";
            return RedirectToAction("ContentManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AssignReviewer(AssignReviewerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Content and reviewer are required.";
                return RedirectToAction("ContentManagement");
            }

            var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == model.ContentId);
            if (content == null)
            {
                return HttpNotFound();
            }

            var exists = await _db.ContentReviewerAssignments
                .AnyAsync(a => a.ContentId == model.ContentId && a.ReviewerId == model.ReviewerId && a.IsActive);

            if (!exists)
            {
                _db.ContentReviewerAssignments.Add(new ContentReviewerAssignmentEntity
                {
                    AssignmentId = Guid.NewGuid(),
                    ContentId = model.ContentId,
                    ReviewerId = model.ReviewerId,
                    AssignedByUserId = User.Identity.GetUserId(),
                    AssignedDate = DateTime.UtcNow,
                    IsActive = true
                });

                _db.Reviews.Add(new ReviewEntity
                {
                    ReviewId = Guid.NewGuid(),
                    ContentId = model.ContentId,
                    ReviewerId = model.ReviewerId,
                    Status = ReviewStatuses.Pending,
                    SubmittedDate = DateTime.UtcNow
                });

                if (content.Status == ContentStatuses.Draft)
                {
                    var oldStatus = content.Status;
                    content.Status = ContentStatuses.UnderReview;
                    content.LastModifiedDate = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    await _audit.LogAsync(User.Identity.GetUserId(), AuditActions.StatusChange, "Content", content.ContentId, oldStatus, content.Status, Request.UserHostAddress, "Admin assigned reviewer and moved content to UnderReview.");
                }
                else
                {
                    await _db.SaveChangesAsync();
                }

                await _audit.LogAsync(User.Identity.GetUserId(), AuditActions.AssignReviewer, "Content", model.ContentId, null, model.ReviewerId, Request.UserHostAddress, "Admin manually assigned reviewer.");
                await _notifications.NotifyReviewerAssignedAsync(content, model.ReviewerId);
                await _versions.SaveSnapshotAsync(model.ContentId, "ASSIGN_REVIEWER", User.Identity.GetUserId(), "Reviewer assigned by administrator.");
            }

            TempData["SuccessMessage"] = "Reviewer assignment saved.";
            return RedirectToAction("ContentManagement");
        }

        private async Task PopulateReviewerOptionsAsync()
        {
            var reviewerRoleId = await _db.Roles.Where(r => r.Name == RoleNames.Reviewer).Select(r => r.Id).SingleOrDefaultAsync();
            ViewBag.ReviewerOptions = await _db.Users
                .Where(u => u.IsActive && u.Roles.Any(role => role.RoleId == reviewerRoleId))
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName + " (" + u.Email + ")"
                })
                .ToListAsync();
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
