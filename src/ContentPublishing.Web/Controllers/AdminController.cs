using System.Web.Mvc;
using System;
using System.Collections.Generic;
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
        private const string QueueApproveAction = "ADMIN_APPROVE_FOR_QUEUE";
        private const string QueueReadyAction = "ADMIN_MARK_READY";
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
            var roleDescriptions = RoleDescriptions();
            var users = await _db.Users
                .OrderBy(u => u.FullName)
                .Select(u => new UserListItemViewModel
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Description = u.Description,
                    Email = u.Email,
                    IsActive = u.IsActive,
                    AssignedRoleName = u.RoleId,
                    Roles = u.Roles.Select(r => r.RoleId).ToList()
                })
                .ToListAsync();

            foreach (var user in users)
            {
                user.Roles = user.Roles.Select(roleId => roles.ContainsKey(roleId) ? roles[roleId] : "Unknown").ToList();
                if (!string.IsNullOrWhiteSpace(user.AssignedRoleName) && roles.ContainsKey(user.AssignedRoleName))
                {
                    user.AssignedRoleName = roles[user.AssignedRoleName];
                }
                else
                {
                    user.AssignedRoleName = null;
                }

                if (string.IsNullOrWhiteSpace(user.AssignedRoleName))
                {
                    user.AssignedRoleName = user.Roles.FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(user.AssignedRoleName) && roleDescriptions.ContainsKey(user.AssignedRoleName))
                {
                    user.AssignedRoleDescription = roleDescriptions[user.AssignedRoleName];
                }
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

            if (roleName != RoleNames.Author && roleName != RoleNames.Reviewer && roleName != RoleNames.Administrator)
            {
                TempData["ErrorMessage"] = "Unsupported role selection.";
                return RedirectToAction("Users");
            }

            var userManager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            var existingRoles = await userManager.GetRolesAsync(userId);

            var rolesToRemove = existingRoles
                .Where(existing => existing == RoleNames.Author || existing == RoleNames.Reviewer || existing == RoleNames.Administrator)
                .Where(existing => roleName == RoleNames.Author || existing != RoleNames.Administrator && existing != RoleNames.Reviewer)
                .ToList();

            foreach (var existing in rolesToRemove)
            {
                await userManager.RemoveFromRoleAsync(userId, existing);
            }

            if (!existingRoles.Contains(roleName))
            {
                var addResult = await userManager.AddToRoleAsync(userId, roleName);
                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Unable to assign selected role: " + string.Join("; ", addResult.Errors);
                    return RedirectToAction("Users");
                }
            }

            var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.RoleId = role.Id;
                user.LastModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            await _db.Database.ExecuteSqlCommandAsync(
                @"UPDATE ur
SET ur.[Description] = COALESCE(r.[Description], r.[Name])
FROM [dbo].[AspNetUserRoles] ur
INNER JOIN [dbo].[AspNetRoles] r ON r.[Id] = ur.[RoleId]
WHERE ur.[UserId] = @p0 AND ur.[RoleId] = @p1;",
                userId,
                role.Id);

            await _audit.LogAsync(User.Identity.GetUserId(), AuditActions.Update, "User", Guid.Empty, string.Join(",", existingRoles), roleName, Request.UserHostAddress, "Admin changed user role.");

            TempData["SuccessMessage"] = "Role updated successfully.";

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

            var approvedItems = await _db.Contents
                .Where(c => c.Status == ContentStatuses.Approved)
                .OrderByDescending(c => c.LastModifiedDate)
                .Select(c => new
                {
                    c.ContentId,
                    c.Title,
                    c.AuthorId,
                    c.LastModifiedDate,
                    ChapterCount = c.Chapters.Count(ch => !ch.IsDeleted)
                })
                .ToListAsync();

            var authorIds = approvedItems.Select(c => c.AuthorId).Distinct().ToList();
            var authorNames = await _db.Users
                .Where(u => authorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.Email })
                .ToListAsync();
            var authorMap = authorNames.ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName);

            var contentIds = approvedItems.Select(c => c.ContentId).ToList();
            var queueEvents = await _db.ContentVersions
                .Where(v => contentIds.Contains(v.ContentId) && (v.Action == QueueApproveAction || v.Action == QueueReadyAction))
                .Select(v => new
                {
                    v.ContentId,
                    v.Action,
                    v.VersionNumber,
                    v.CreatedDate
                })
                .ToListAsync();

            var latestQueueActionByContent = queueEvents
                .GroupBy(v => v.ContentId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(v => v.VersionNumber)
                        .ThenByDescending(v => v.CreatedDate)
                        .Select(v => v.Action)
                        .FirstOrDefault());

            var pendingItems = new List<AdminQueueItemViewModel>();
            var awaitingPreviewItems = new List<AdminQueueItemViewModel>();
            var completedPreviewItems = new List<AdminQueueItemViewModel>();

            foreach (var content in approvedItems)
            {
                latestQueueActionByContent.TryGetValue(content.ContentId, out var latestQueueAction);
                var item = new AdminQueueItemViewModel
                {
                    ContentId = content.ContentId,
                    Title = content.Title,
                    AuthorName = authorMap.ContainsKey(content.AuthorId) ? authorMap[content.AuthorId] : content.AuthorId,
                    LastModifiedDate = content.LastModifiedDate,
                    ChapterCount = content.ChapterCount,
                    QueueStatusLabel = ResolveQueueLabel(latestQueueAction)
                };

                if (latestQueueAction == QueueReadyAction)
                {
                    completedPreviewItems.Add(item);
                }
                else if (latestQueueAction == QueueApproveAction)
                {
                    awaitingPreviewItems.Add(item);
                }
                else
                {
                    pendingItems.Add(item);
                }
            }

            var model = new AdminContentQueueViewModel
            {
                PendingItems = pendingItems,
                AwaitingPreviewItems = awaitingPreviewItems,
                CompletedPreviewItems = completedPreviewItems
            };

            return View(model);
        }

        public async Task<ActionResult> Preview(Guid id)
        {
            if (!User.IsInRole(RoleNames.Administrator))
            {
                return new HttpUnauthorizedResult();
            }

            var content = await _db.Contents
                .Include(c => c.Chapters)
                .SingleOrDefaultAsync(c => c.ContentId == id);

            if (content == null)
            {
                return HttpNotFound();
            }

            if (content.Status != ContentStatuses.Approved)
            {
                TempData["ErrorMessage"] = "Only approved content can be previewed in the queue.";
                return RedirectToAction("ContentManagement");
            }

            var author = await _db.Users
                .Where(u => u.Id == content.AuthorId)
                .Select(u => new { u.FullName, u.Email })
                .SingleOrDefaultAsync();

            var latestQueueAction = await GetLatestQueueActionAsync(content.ContentId);

            var model = new AdminContentPreviewViewModel
            {
                ContentId = content.ContentId,
                Title = content.Title,
                Description = content.Description,
                AuthorName = author == null ? content.AuthorId : (string.IsNullOrWhiteSpace(author.FullName) ? author.Email : author.FullName),
                LastModifiedDate = content.LastModifiedDate,
                QueueStatusLabel = ResolveQueueLabel(latestQueueAction),
                CanApproveForQueue = string.IsNullOrWhiteSpace(latestQueueAction),
                CanMarkAsReady = latestQueueAction == QueueApproveAction,
                Chapters = content.Chapters
                    .Where(ch => !ch.IsDeleted)
                    .OrderBy(ch => ch.ChapterOrder)
                    .Select(ch => new AdminPreviewChapterItemViewModel
                    {
                        ChapterTitle = ch.ChapterTitle,
                        ChapterBody = ch.ChapterBody,
                        ChapterOrder = ch.ChapterOrder
                    })
                    .ToList()
            };

            ViewBag.SuccessMessage = "Preview opened. Review the full article/handbook before marking it ready.";

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ApproveForQueue(Guid id)
        {
            var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == id);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (content.Status != ContentStatuses.Approved)
            {
                TempData["ErrorMessage"] = "Only approved content can be added to queue.";
                return RedirectToAction("ContentManagement");
            }

            var latestQueueAction = await GetLatestQueueActionAsync(id);
            if (latestQueueAction == QueueApproveAction || latestQueueAction == QueueReadyAction)
            {
                TempData["ErrorMessage"] = "This content is already in the admin queue workflow.";
                return RedirectToAction("ContentManagement");
            }

            await _versions.SaveSnapshotAsync(id, QueueApproveAction, User.Identity.GetUserId(), "Admin approved content for publishing queue.");
            await _audit.LogAsync(User.Identity.GetUserId(), AuditActions.Update, "Content", id, null, QueueApproveAction, Request.UserHostAddress, "Admin approved content for queue.");

            TempData["SuccessMessage"] = "Content approved for queue. Preview is required before marking ready.";
            return RedirectToAction("ContentManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> MarkAsReady(Guid id)
        {
            var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == id);
            if (content == null)
            {
                return HttpNotFound();
            }

            if (content.Status != ContentStatuses.Approved)
            {
                TempData["ErrorMessage"] = "Only approved content can be marked as ready.";
                return RedirectToAction("ContentManagement");
            }

            var latestQueueAction = await GetLatestQueueActionAsync(id);
            if (latestQueueAction != QueueApproveAction)
            {
                TempData["ErrorMessage"] = "Approve content for queue first, then preview, before marking ready.";
                return RedirectToAction("ContentManagement");
            }

            await _versions.SaveSnapshotAsync(id, QueueReadyAction, User.Identity.GetUserId(), "Admin completed preview and marked content ready.");
            await _audit.LogAsync(User.Identity.GetUserId(), AuditActions.Update, "Content", id, QueueApproveAction, QueueReadyAction, Request.UserHostAddress, "Admin marked queued content as ready.");

            TempData["SuccessMessage"] = "Content marked as ready after preview.";
            return RedirectToAction("ContentManagement");
        }

        public async Task<ActionResult> ReviewerMetrics()
        {
            var reviewerRoleId = await _db.Roles
                .Where(r => r.Name == RoleNames.Reviewer)
                .Select(r => r.Id)
                .SingleOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(reviewerRoleId))
            {
                return View(new ReviewerMetricsViewModel
                {
                    GeneratedAtUtc = DateTime.UtcNow,
                    Reviewers = new System.Collections.Generic.List<ReviewerMetricListItemViewModel>()
                });
            }

            var reviewers = await _db.Users
                .Where(u => u.Roles.Any(ur => ur.RoleId == reviewerRoleId) || u.RoleId == reviewerRoleId)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email
                })
                .ToListAsync();

            var reviewerIds = reviewers.Select(r => r.Id).ToList();
            var reviews = await _db.Reviews
                .Where(r => reviewerIds.Contains(r.ReviewerId))
                .Select(r => new
                {
                    r.ReviewerId,
                    r.Status,
                    r.SubmittedDate,
                    r.ReviewDate
                })
                .ToListAsync();

            var metrics = reviewers
                .Select(reviewer =>
                {
                    var reviewerReviews = reviews.Where(r => r.ReviewerId == reviewer.Id).ToList();
                    var pending = reviewerReviews.Count(r => r.Status == ReviewStatuses.Pending);
                    var completed = reviewerReviews.Count(r => r.Status == ReviewStatuses.Approved || r.Status == ReviewStatuses.Rejected);
                    var approved = reviewerReviews.Count(r => r.Status == ReviewStatuses.Approved);
                    var rejected = reviewerReviews.Count(r => r.Status == ReviewStatuses.Rejected);

                    var completedWithTime = reviewerReviews
                        .Where(r => r.ReviewDate.HasValue && (r.Status == ReviewStatuses.Approved || r.Status == ReviewStatuses.Rejected))
                        .ToList();

                    var averageHours = completedWithTime.Any()
                        ? (int)Math.Round(completedWithTime.Average(r => (r.ReviewDate.Value - r.SubmittedDate).TotalHours))
                        : 0;

                    var approvalRate = completed > 0
                        ? (int)Math.Round((double)approved * 100 / completed)
                        : 0;

                    return new ReviewerMetricListItemViewModel
                    {
                        ReviewerId = reviewer.Id,
                        ReviewerName = string.IsNullOrWhiteSpace(reviewer.FullName) ? reviewer.Email : reviewer.FullName,
                        ReviewerEmail = reviewer.Email,
                        PendingCount = pending,
                        CompletedCount = completed,
                        ApprovedCount = approved,
                        RejectedCount = rejected,
                        AverageTurnaroundHours = averageHours,
                        ApprovalRate = approvalRate
                    };
                })
                .OrderByDescending(r => r.PendingCount)
                .ThenBy(r => r.ReviewerName)
                .ToList();

            var model = new ReviewerMetricsViewModel
            {
                GeneratedAtUtc = DateTime.UtcNow,
                Reviewers = metrics
            };

            return View(model);
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

        private async Task<string> GetLatestQueueActionAsync(Guid contentId)
        {
            return await _db.ContentVersions
                .Where(v => v.ContentId == contentId && (v.Action == QueueApproveAction || v.Action == QueueReadyAction))
                .OrderByDescending(v => v.VersionNumber)
                .ThenByDescending(v => v.CreatedDate)
                .Select(v => v.Action)
                .FirstOrDefaultAsync();
        }

        private static string ResolveQueueLabel(string queueAction)
        {
            if (queueAction == QueueReadyAction)
            {
                return "Completed Preview";
            }

            if (queueAction == QueueApproveAction)
            {
                return "Approved Awaiting Preview";
            }

            return "Pending";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }

            base.Dispose(disposing);
        }

        private static System.Collections.Generic.Dictionary<string, string> RoleDescriptions()
        {
            return new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { RoleNames.Author, "Creates and submits draft content for review." },
                { RoleNames.Reviewer, "Reviews author submissions and approves or rejects with notes." },
                { RoleNames.Administrator, "Manages users, role assignments, and publication workflow." }
            };
        }
    }
}
