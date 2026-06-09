using ContentPublishing.Web.Net8.Data;
using ContentPublishing.Web.Net8.Models;
using ContentPublishing.Web.Net8.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ContentPublishing.Web.Net8.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ContentReadDbContext _db;
    private readonly AppIdentityDbContext _identityDb;

    public AdminController(ContentReadDbContext db, AppIdentityDbContext identityDb)
    {
        _db = db;
        _identityDb = identityDb;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var model = new AdminDashboardViewModel
        {
            TotalContent = await _db.Contents.CountAsync(),
            DraftCount = await _db.Contents.CountAsync(c => c.Status == "Draft"),
            UnderReviewCount = await _db.Contents.CountAsync(c => c.Status == "UnderReview"),
            ApprovedCount = await _db.Contents.CountAsync(c => c.Status == "Approved"),
            PublishedCount = await _db.Contents.CountAsync(c => c.Status == "Published")
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ContentManagement()
    {
        var reviewerRoleId = await _identityDb.Roles
            .AsNoTracking()
            .Where(r => r.Name == "Reviewer")
            .Select(r => r.Id)
            .SingleOrDefaultAsync();

        var reviewerIds = string.IsNullOrWhiteSpace(reviewerRoleId)
            ? new List<string>()
            : await _identityDb.UserRoles
                .AsNoTracking()
                .Where(ur => ur.RoleId == reviewerRoleId)
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

        var reviewerLookup = reviewerIds.Count == 0
            ? new Dictionary<string, string>()
            : await _identityDb.Users
                .AsNoTracking()
                .Where(u => reviewerIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => !string.IsNullOrWhiteSpace(u.Email) ? u.Email : (u.UserName ?? "Reviewer"));

        var reviewerOptions = reviewerLookup
            .Select(kvp => new AdminReviewerOptionViewModel
            {
                ReviewerId = kvp.Key,
                DisplayName = kvp.Value
            })
            .OrderBy(o => o.DisplayName)
            .ToList();

        var readyToPublish = await _db.Contents
            .AsNoTracking()
            .Where(c => c.Status == "Approved")
            .OrderByDescending(c => c.LastModifiedDate)
            .Select(c => new AdminContentItemViewModel
            {
                ContentId = c.ContentId,
                Title = c.Title,
                Status = c.Status,
                LastModifiedDate = c.LastModifiedDate,
                PublishedDate = c.PublishedDate
            })
            .ToListAsync();

        var recentPublished = await _db.Contents
            .AsNoTracking()
            .Where(c => c.Status == "Published")
            .OrderByDescending(c => c.PublishedDate)
            .Take(20)
            .Select(c => new AdminContentItemViewModel
            {
                ContentId = c.ContentId,
                Title = c.Title,
                Status = c.Status,
                LastModifiedDate = c.LastModifiedDate,
                PublishedDate = c.PublishedDate
            })
            .ToListAsync();

        var pendingWorkRecords = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.Status == "Pending")
            .Join(
                _db.Contents.AsNoTracking().Where(c => c.Status == "UnderReview"),
                review => review.ContentId,
                content => content.ContentId,
                (review, content) => new
                {
                    review.ReviewId,
                    review.ContentId,
                    ContentTitle = content.Title,
                    review.ReviewerId,
                    review.SubmittedDate
                })
            .OrderBy(r => r.SubmittedDate)
            .ToListAsync();

        var pendingWorkItems = pendingWorkRecords
            .Select(r => new AdminPendingWorkItemViewModel
            {
                ReviewId = r.ReviewId,
                ContentId = r.ContentId,
                ContentTitle = r.ContentTitle,
                ReviewerId = r.ReviewerId,
                ReviewerName = reviewerLookup.TryGetValue(r.ReviewerId, out var name) ? name : "Reviewer",
                SubmittedDate = r.SubmittedDate
            })
            .ToList();

        return View(new AdminContentManagementViewModel
        {
            ReadyToPublish = readyToPublish,
            RecentPublished = recentPublished,
            PendingWorkItems = pendingWorkItems,
            ReviewerOptions = reviewerOptions
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReassignWork(Guid reviewId, string reviewerId)
    {
        if (reviewId == Guid.Empty || string.IsNullOrWhiteSpace(reviewerId))
        {
            TempData["ErrorMessage"] = "Review work reassignment request is invalid.";
            return RedirectToAction(nameof(ContentManagement));
        }

        var targetRoleId = await _identityDb.Roles
            .AsNoTracking()
            .Where(r => r.Name == "Reviewer")
            .Select(r => r.Id)
            .SingleOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(targetRoleId))
        {
            TempData["ErrorMessage"] = "Reviewer role is not configured.";
            return RedirectToAction(nameof(ContentManagement));
        }

        var isReviewer = await _identityDb.UserRoles
            .AsNoTracking()
            .AnyAsync(ur => ur.UserId == reviewerId && ur.RoleId == targetRoleId);
        if (!isReviewer)
        {
            TempData["ErrorMessage"] = "Selected user is not in the Reviewer role.";
            return RedirectToAction(nameof(ContentManagement));
        }

        var review = await _db.Reviews.SingleOrDefaultAsync(r => r.ReviewId == reviewId);
        if (review == null)
        {
            TempData["ErrorMessage"] = "Review work item not found.";
            return RedirectToAction(nameof(ContentManagement));
        }

        if (!string.Equals(review.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only pending work items can be reassigned.";
            return RedirectToAction(nameof(ContentManagement));
        }

        var previousReviewerId = review.ReviewerId;
        if (string.Equals(previousReviewerId, reviewerId, StringComparison.Ordinal))
        {
            TempData["SuccessMessage"] = "Work item is already assigned to the selected reviewer.";
            return RedirectToAction(nameof(ContentManagement));
        }

        review.ReviewerId = reviewerId;
        review.SubmittedDate = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = AuditActions.Update,
            EntityType = "Review",
            EntityId = review.ReviewId,
            OldValue = previousReviewerId,
            NewValue = reviewerId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ChangeDetails = "Admin reassigned pending review work item."
        });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Work item reassigned successfully.";
        return RedirectToAction(nameof(ContentManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid contentId)
    {
        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == contentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(ContentManagement));
        }

        if (!string.Equals(content.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only Approved content can be published.";
            return RedirectToAction(nameof(ContentManagement));
        }

        var previousStatus = content.Status;
        content.Status = "Published";
        content.PublishedDate = DateTime.UtcNow;
        content.LastModifiedDate = DateTime.UtcNow;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = userId,
            Action = AuditActions.Publish,
            EntityType = "Content",
            EntityId = contentId,
            OldValue = previousStatus,
            NewValue = "Published",
            IpAddress = ip,
            ChangeDetails = "Admin published approved content."
        });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Content published successfully.";
        return RedirectToAction(nameof(ContentManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveToDraft(Guid contentId)
    {
        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == contentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(ContentManagement));
        }

        var previousStatus = content.Status;
        content.Status = "Draft";
        content.PublishedDate = null;
        content.LastModifiedDate = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = AuditActions.StatusChange,
            EntityType = "Content",
            EntityId = contentId,
            OldValue = previousStatus,
            NewValue = "Draft",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ChangeDetails = "Admin moved content back to draft."
        });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Content moved back to Draft.";
        return RedirectToAction(nameof(ContentManagement));
    }
}