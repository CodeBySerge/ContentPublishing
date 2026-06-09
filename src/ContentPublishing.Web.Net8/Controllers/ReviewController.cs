using ContentPublishing.Web.Net8.Data;
using ContentPublishing.Web.Net8.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ContentPublishing.Web.Net8.Controllers;

[Authorize(Roles = "Reviewer")]
public class ReviewController : Controller
{
    private const string ClarificationPrefix = "CLARIFICATION_REQUEST::";
    private readonly ContentReadDbContext _db;

    public ReviewController(ContentReadDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var pendingReviewContentIds = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewerId == reviewerId && r.Status == "Pending")
            .OrderBy(r => r.SubmittedDate)
            .Select(r => r.ContentId)
            .ToListAsync();

        var nextCandidates = await _db.Contents
            .AsNoTracking()
            .Where(c => c.Status == "UnderReview" && pendingReviewContentIds.Contains(c.ContentId))
            .OrderBy(c => c.LastModifiedDate)
            .Take(10)
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

        var model = new ReviewerDashboardViewModel
        {
            UnderReviewCount = nextCandidates.Count,
            PublishedCount = await _db.Contents.CountAsync(c => c.Status == "Published"),
            NextReviewCandidates = nextCandidates
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PendingReviews()
    {
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var chapterLookup = await _db.Chapters
            .AsNoTracking()
            .Where(ch => !ch.IsDeleted)
            .GroupBy(ch => ch.ContentId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        var pendingReviewContentIds = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewerId == reviewerId && r.Status == "Pending")
            .OrderBy(r => r.SubmittedDate)
            .Select(r => r.ContentId)
            .ToListAsync();

        var items = await _db.Contents
            .AsNoTracking()
            .Where(c => c.Status == "UnderReview" && pendingReviewContentIds.Contains(c.ContentId))
            .OrderBy(c => c.LastModifiedDate)
            .Select(c => new PendingReviewItemViewModel
            {
                ContentId = c.ContentId,
                Title = c.Title,
                Status = c.Status,
                LastModifiedDate = c.LastModifiedDate,
                ChapterCount = 0
            })
            .ToListAsync();

        if (!items.Any())
        {
            items = await _db.Contents
                .AsNoTracking()
                .Where(c => c.Status == "UnderReview")
                .OrderBy(c => c.LastModifiedDate)
                .Select(c => new PendingReviewItemViewModel
                {
                    ContentId = c.ContentId,
                    Title = c.Title,
                    Status = c.Status,
                    LastModifiedDate = c.LastModifiedDate,
                    ChapterCount = 0
                })
                .ToListAsync();
        }

        var model = items
            .Select(i => new PendingReviewItemViewModel
            {
                ContentId = i.ContentId,
                Title = i.Title,
                Status = i.Status,
                LastModifiedDate = i.LastModifiedDate,
                ChapterCount = chapterLookup.TryGetValue(i.ContentId, out var count) ? count : 0
            })
            .ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ReviewContent(Guid contentId)
    {
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var content = await _db.Contents
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.ContentId == contentId);

        if (content == null)
        {
            return NotFound();
        }

        var chapters = await _db.Chapters
            .AsNoTracking()
            .Where(ch => ch.ContentId == contentId && !ch.IsDeleted)
            .OrderBy(ch => ch.ChapterOrder)
            .Select(ch => new ReviewChapterItemViewModel
            {
                ChapterId = ch.ChapterId,
                ChapterOrder = ch.ChapterOrder,
                ChapterTitle = ch.ChapterTitle,
                ChapterBody = ch.ChapterBody
            })
            .ToListAsync();

        var existingReview = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ContentId == contentId && r.ReviewerId == reviewerId)
            .OrderByDescending(r => r.SubmittedDate)
            .FirstOrDefaultAsync();

        if (existingReview == null && string.Equals(content.Status, "UnderReview", StringComparison.OrdinalIgnoreCase))
        {
            _db.Reviews.Add(new ReviewRecord
            {
                ReviewId = Guid.NewGuid(),
                ContentId = contentId,
                ReviewerId = reviewerId,
                Status = "Pending",
                SubmittedDate = DateTime.UtcNow,
                AuthorChangeNotes = null
            });
            await _db.SaveChangesAsync();
        }

        var model = new ReviewContentViewModel
        {
            ContentId = content.ContentId,
            Title = content.Title,
            Description = content.Description ?? string.Empty,
            ContentStatus = content.Status,
            LastModifiedDate = content.LastModifiedDate,
            ExistingComments = existingReview?.Comments,
            AuthorChangeNotes = existingReview?.AuthorChangeNotes,
            Chapters = chapters
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ReviewDecisionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid review request.";
            return RedirectToAction(nameof(PendingReviews));
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == model.ContentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(PendingReviews));
        }

        if (!string.Equals(content.Status, "UnderReview", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only UnderReview content can be approved.";
            return RedirectToAction(nameof(ReviewContent), new { contentId = model.ContentId });
        }

        var review = await _db.Reviews
            .Where(r => r.ContentId == model.ContentId && r.ReviewerId == reviewerId && r.Status == "Pending")
            .OrderByDescending(r => r.SubmittedDate)
            .FirstOrDefaultAsync();
        if (review == null)
        {
            review = new ReviewRecord
            {
                ReviewId = Guid.NewGuid(),
                ContentId = model.ContentId,
                ReviewerId = reviewerId,
                Status = "Pending",
                SubmittedDate = DateTime.UtcNow
            };
            _db.Reviews.Add(review);
        }

        review.Status = "Approved";
        review.Comments = string.IsNullOrWhiteSpace(model.Comments) ? review.Comments : model.Comments.Trim();
        review.ReviewDate = DateTime.UtcNow;

        var hasOtherPending = await _db.Reviews.AnyAsync(r => r.ContentId == model.ContentId && r.Status == "Pending" && r.ReviewId != review.ReviewId);
        var previousStatus = content.Status;
        if (!hasOtherPending)
        {
            content.Status = "Approved";
        }
        content.LastModifiedDate = DateTime.UtcNow;

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = reviewerId,
            Action = AuditActions.Approve,
            EntityType = "Review",
            EntityId = review.ReviewId,
            OldValue = "Pending",
            NewValue = "Approved",
            IpAddress = ip,
            ChangeDetails = "Reviewer approved content."
        });

        if (!hasOtherPending && !string.Equals(previousStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            _db.AuditLogs.Add(new AuditLogRecord
            {
                LogId = Guid.NewGuid(),
                UserId = reviewerId,
                Action = AuditActions.StatusChange,
                EntityType = "Content",
                EntityId = model.ContentId,
                OldValue = previousStatus,
                NewValue = "Approved",
                IpAddress = ip,
                ChangeDetails = "All assigned reviewers approved content."
            });
        }

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = hasOtherPending
            ? "Approval recorded. Waiting for remaining reviewers."
            : "Content approved and moved to Admin publishing queue.";
        return RedirectToAction(nameof(PendingReviews));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ReviewDecisionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid review request.";
            return RedirectToAction(nameof(PendingReviews));
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == model.ContentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(PendingReviews));
        }

        if (!string.Equals(content.Status, "UnderReview", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only UnderReview content can be rejected.";
            return RedirectToAction(nameof(ReviewContent), new { contentId = model.ContentId });
        }

        var review = await _db.Reviews
            .Where(r => r.ContentId == model.ContentId && r.ReviewerId == reviewerId && r.Status == "Pending")
            .OrderByDescending(r => r.SubmittedDate)
            .FirstOrDefaultAsync();
        if (review == null)
        {
            review = new ReviewRecord
            {
                ReviewId = Guid.NewGuid(),
                ContentId = model.ContentId,
                ReviewerId = reviewerId,
                Status = "Pending",
                SubmittedDate = DateTime.UtcNow
            };
            _db.Reviews.Add(review);
        }

        review.Status = "Rejected";
        review.Comments = string.IsNullOrWhiteSpace(model.Comments) ? "Rejected" : model.Comments.Trim();
        review.ReviewDate = DateTime.UtcNow;

        var previousStatus = content.Status;
        content.Status = "Draft";
        content.LastModifiedDate = DateTime.UtcNow;

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = reviewerId,
            Action = AuditActions.Reject,
            EntityType = "Review",
            EntityId = review.ReviewId,
            OldValue = "Pending",
            NewValue = "Rejected",
            IpAddress = ip,
            ChangeDetails = "Reviewer rejected content."
        });
        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = reviewerId,
            Action = AuditActions.StatusChange,
            EntityType = "Content",
            EntityId = model.ContentId,
            OldValue = previousStatus,
            NewValue = "Draft",
            IpAddress = ip,
            ChangeDetails = "Rejected content returned to Draft."
        });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Content rejected and returned to Draft.";
        return RedirectToAction(nameof(PendingReviews));
    }

    [HttpGet]
    public async Task<IActionResult> Notifications()
    {
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var assignments = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewerId == reviewerId && r.Status == "Pending")
            .Join(
                _db.Contents.AsNoTracking().Where(c => c.Status == "UnderReview"),
                review => review.ContentId,
                content => content.ContentId,
                (review, content) => new ReviewNotificationListItemViewModel
                {
                    ContentId = content.ContentId,
                    NotificationType = "Assignment",
                    Title = content.Title,
                    Message = "New draft assigned for review.",
                    CreatedDate = review.SubmittedDate
                })
            .ToListAsync();

        var clarifications = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewerId == reviewerId && r.Status == "Pending" && !string.IsNullOrWhiteSpace(r.AuthorChangeNotes))
            .Join(
                _db.Contents.AsNoTracking(),
                review => review.ContentId,
                content => content.ContentId,
                (review, content) => new ReviewNotificationListItemViewModel
                {
                    ContentId = content.ContentId,
                    NotificationType = "Author Clarification",
                    Title = content.Title,
                    Message = review.AuthorChangeNotes ?? string.Empty,
                    CreatedDate = review.SubmittedDate
                })
            .ToListAsync();

        var items = assignments
            .Concat(clarifications)
            .OrderByDescending(i => i.CreatedDate)
            .Take(50)
            .ToList();

        return View(new ReviewNotificationsViewModel
        {
            TotalNotifications = items.Count,
            Items = items
        });
    }

    [HttpGet]
    public async Task<IActionResult> ReviewHistory()
    {
        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var history = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewerId == reviewerId)
            .Join(
                _db.Contents.AsNoTracking(),
                review => review.ContentId,
                content => content.ContentId,
                (review, content) => new ReviewHistoryItemViewModel
                {
                    ReviewId = review.ReviewId,
                    ContentId = review.ContentId,
                    Title = content.Title,
                    Status = review.Status,
                    Comments = review.Comments,
                    SubmittedDate = review.SubmittedDate,
                    ReviewDate = review.ReviewDate
                })
            .OrderByDescending(r => r.SubmittedDate)
            .Take(100)
            .ToListAsync();

        return View(history);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestClarification(ReviewClarificationRequestViewModel model)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Message))
        {
            TempData["ErrorMessage"] = "Clarification request note is required.";
            return RedirectToAction(nameof(ReviewContent), new { contentId = model.ContentId });
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var content = await _db.Contents.SingleOrDefaultAsync(c => c.ContentId == model.ContentId);
        if (content == null)
        {
            TempData["ErrorMessage"] = "Content not found.";
            return RedirectToAction(nameof(PendingReviews));
        }

        if (string.Equals(content.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Archived content cannot receive clarification requests.";
            return RedirectToAction(nameof(PendingReviews));
        }

        var review = await _db.Reviews
            .Where(r => r.ContentId == model.ContentId && r.ReviewerId == reviewerId && r.Status == "Pending")
            .OrderByDescending(r => r.SubmittedDate)
            .FirstOrDefaultAsync();

        if (review == null)
        {
            review = new ReviewRecord
            {
                ReviewId = Guid.NewGuid(),
                ContentId = model.ContentId,
                ReviewerId = reviewerId,
                Status = "Pending",
                SubmittedDate = DateTime.UtcNow
            };
            _db.Reviews.Add(review);
        }

        var message = model.Message.Trim();
        if (message.Length > 2000)
        {
            message = message.Substring(0, 2000);
        }

        review.Comments = ClarificationPrefix + message;

        _db.AuditLogs.Add(new AuditLogRecord
        {
            LogId = Guid.NewGuid(),
            UserId = reviewerId,
            Action = AuditActions.Update,
            EntityType = "Review",
            EntityId = review.ReviewId,
            OldValue = null,
            NewValue = "CLARIFICATION_REQUESTED",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ChangeDetails = "Reviewer requested clarification from author."
        });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Clarification requested from author.";
        return RedirectToAction(nameof(ReviewContent), new { contentId = model.ContentId });
    }
}