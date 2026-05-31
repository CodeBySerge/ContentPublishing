using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using ContentPublishing.Application.Rules;
using ContentPublishing.Web.Models;
using ContentPublishing.Web.Services;
using ContentPublishing.Web.Security;
using ContentPublishing.Web.ViewModels;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace ContentPublishing.Web.Controllers
{
    [Authorize(Roles = RoleNames.Reviewer)]
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly AuditLogService _audit;
        private readonly WorkflowNotificationService _notifications;
        private readonly ContentVersionService _versions;

        public ReviewController()
        {
            _audit = new AuditLogService(_db);
            _notifications = new WorkflowNotificationService(_db, new SmtpEmailService());
            _versions = new ContentVersionService(_db);
        }

        public async Task<ActionResult> PendingReviews()
        {
            var reviewerId = User.Identity.GetUserId();

            var pendingItems = await _db.Reviews
                .Where(r => r.ReviewerId == reviewerId && r.Status == ReviewStatuses.Pending && r.Content.Status == ContentStatuses.UnderReview)
                .OrderBy(r => r.SubmittedDate)
                .Select(r => new PendingReviewListItemViewModel
                {
                    ContentId = r.ContentId,
                    ReviewId = r.ReviewId,
                    Title = r.Content.Title,
                    Status = r.Content.Status,
                    SubmittedDate = r.SubmittedDate,
                    ChapterCount = r.Content.Chapters.Count(ch => !ch.IsDeleted)
                })
                .ToListAsync();

            return View(pendingItems);
        }

        public async Task<ActionResult> ReviewContent(Guid contentId)
        {
            var reviewerId = User.Identity.GetUserId();

            var review = await _db.Reviews
                .Include(r => r.Content)
                .Include(r => r.Content.Chapters)
                .SingleOrDefaultAsync(r => r.ContentId == contentId && r.ReviewerId == reviewerId);

            if (review == null)
            {
                return HttpNotFound();
            }

            var model = new ReviewContentViewModel
            {
                ContentId = review.ContentId,
                ReviewId = review.ReviewId,
                Title = review.Content.Title,
                Description = review.Content.Description,
                ContentStatus = review.Content.Status,
                ExistingComments = review.Comments,
                AuthorChangeNotes = review.AuthorChangeNotes,
                HighlightedChangesHtml = await BuildHighlightedChangesHtmlAsync(review.ContentId),
                Chapters = review.Content.Chapters
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

        public async Task<ActionResult> ReviewHistory()
        {
            var reviewerId = User.Identity.GetUserId();

            var history = await _db.Reviews
                .Where(r => r.ReviewerId == reviewerId)
                .OrderByDescending(r => r.SubmittedDate)
                .Select(r => new ReviewHistoryItemViewModel
                {
                    ReviewId = r.ReviewId,
                    ContentId = r.ContentId,
                    Title = r.Content.Title,
                    Status = r.Status,
                    Comments = r.Comments,
                    SubmittedDate = r.SubmittedDate,
                    ReviewDate = r.ReviewDate
                })
                .ToListAsync();

            return View(history);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Approve(ReviewDecisionViewModel model)
        {
            var reviewerId = User.Identity.GetUserId();
            var review = await _db.Reviews
                .Include(r => r.Content)
                .SingleOrDefaultAsync(r => r.ReviewId == model.ReviewId && r.ContentId == model.ContentId && r.ReviewerId == reviewerId);

            if (review == null)
            {
                return HttpNotFound();
            }

            if (!ContentWorkflowRules.CanApprove(review.Content.Status))
            {
                TempData["ErrorMessage"] = "Only content currently under review can be approved.";
                return RedirectToAction("ReviewContent", new { contentId = model.ContentId });
            }

            review.Status = ReviewStatuses.Approved;
            review.Comments = model.Comments;
            review.ReviewDate = DateTime.UtcNow;

            var pendingCount = await _db.Reviews.CountAsync(r => r.ContentId == model.ContentId && r.Status == ReviewStatuses.Pending);
            if (pendingCount == 0)
            {
                var previousStatus = review.Content.Status;
                review.Content.Status = ContentWorkflowRules.ResolveStatusAfterApproval(review.Content.Status, pendingCount);
                review.Content.LastModifiedDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                await _audit.LogAsync(reviewerId, AuditActions.Approve, "Review", review.ReviewId, ReviewStatuses.Pending, ReviewStatuses.Approved, Request.UserHostAddress, "Reviewer approved content.");
                await _audit.LogAsync(reviewerId, AuditActions.StatusChange, "Content", model.ContentId, previousStatus, ContentStatuses.Approved, Request.UserHostAddress, "All assigned reviewers approved content.");
                await _notifications.NotifyContentApprovedAsync(review.Content);
                await _versions.SaveSnapshotAsync(model.ContentId, "APPROVE_CONTENT", reviewerId, "All reviewers approved content.");

                TempData["SuccessMessage"] = "Review approved. Content is now Approved.";
                return RedirectToAction("PendingReviews");
            }

            await _db.SaveChangesAsync();
            await _audit.LogAsync(reviewerId, AuditActions.Approve, "Review", review.ReviewId, ReviewStatuses.Pending, ReviewStatuses.Approved, Request.UserHostAddress, "Reviewer approved content.");

            TempData["SuccessMessage"] = "Your review approval has been recorded.";
            return RedirectToAction("PendingReviews");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Reject(ReviewDecisionViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Comments))
            {
                TempData["ErrorMessage"] = "Rejection note is required.";
                return RedirectToAction("ReviewContent", new { contentId = model.ContentId });
            }

            var reviewerId = User.Identity.GetUserId();
            var review = await _db.Reviews
                .Include(r => r.Content)
                .SingleOrDefaultAsync(r => r.ReviewId == model.ReviewId && r.ContentId == model.ContentId && r.ReviewerId == reviewerId);

            if (review == null)
            {
                return HttpNotFound();
            }

            if (review.Content.Status == ContentStatuses.Archived)
            {
                TempData["ErrorMessage"] = "Archived content cannot be reviewed.";
                return RedirectToAction("PendingReviews");
            }

            var previousStatus = review.Content.Status;
            review.Status = ReviewStatuses.Rejected;
            review.Comments = model.Comments;
            review.ReviewDate = DateTime.UtcNow;

            review.Content.Status = ContentWorkflowRules.ResolveStatusAfterRejection();
            review.Content.LastModifiedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _audit.LogAsync(reviewerId, AuditActions.Reject, "Review", review.ReviewId, ReviewStatuses.Pending, ReviewStatuses.Rejected, Request.UserHostAddress, "Reviewer rejected content with comments.");
            await _audit.LogAsync(reviewerId, AuditActions.StatusChange, "Content", model.ContentId, previousStatus, ContentStatuses.Draft, Request.UserHostAddress, "Rejected content returned to Draft.");
            await _notifications.NotifyContentRejectedAsync(review.Content, model.Comments);
            await _versions.SaveSnapshotAsync(model.ContentId, "REJECT_CONTENT", reviewerId, "Content rejected and returned to draft.");

            TempData["SuccessMessage"] = "Review rejected. Content returned to Draft.";
            return RedirectToAction("PendingReviews");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }

            base.Dispose(disposing);
        }

        private async Task<string> BuildHighlightedChangesHtmlAsync(Guid contentId)
        {
            var versions = await _db.ContentVersions
                .Where(v => v.ContentId == contentId)
                .OrderByDescending(v => v.VersionNumber)
                .Take(2)
                .ToListAsync();

            if (versions.Count < 2)
            {
                return "<p>No previous version available to highlight changes yet.</p>";
            }

            var latest = SafeParseSnapshot(versions[0].SnapshotJson);
            var previous = SafeParseSnapshot(versions[1].SnapshotJson);
            if (latest == null || previous == null)
            {
                return "<p>Unable to compute highlighted changes for this submission.</p>";
            }

            var latestChapters = latest["Chapters"] as JArray ?? new JArray();
            var previousChapters = previous["Chapters"] as JArray ?? new JArray();
            var prevById = previousChapters
                .OfType<JObject>()
                .Where(c => c["ChapterId"] != null)
                .ToDictionary(c => (string)c["ChapterId"], c => c);

            var sb = new StringBuilder();
            sb.Append("<div class='space-y-3'>");

            foreach (var chapter in latestChapters.OfType<JObject>())
            {
                var chapterId = (string)chapter["ChapterId"];
                var chapterTitle = (string)chapter["ChapterTitle"] ?? "Chapter";
                var chapterBody = (string)chapter["ChapterBody"] ?? string.Empty;

                prevById.TryGetValue(chapterId ?? string.Empty, out var previousChapter);
                var previousBody = previousChapter == null ? string.Empty : (string)previousChapter["ChapterBody"] ?? string.Empty;

                var diffHtml = BuildSimpleLineDiff(previousBody, chapterBody);
                if (string.IsNullOrWhiteSpace(diffHtml))
                {
                    continue;
                }

                sb.Append("<section class='rounded border border-slate-200 p-3'>")
                    .Append("<h4 class='mb-2 font-semibold'>")
                    .Append(HttpUtility.HtmlEncode(chapterTitle))
                    .Append("</h4>")
                    .Append("<div class='space-y-1 text-sm'>")
                    .Append(diffHtml)
                    .Append("</div></section>");
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        private static JObject SafeParseSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JObject.Parse(json);
            }
            catch
            {
                return null;
            }
        }

        private static string BuildSimpleLineDiff(string oldText, string newText)
        {
            var oldLines = (oldText ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var newLines = (newText ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var oldSet = new HashSet<string>(oldLines.Where(l => !string.IsNullOrWhiteSpace(l)));
            var newSet = new HashSet<string>(newLines.Where(l => !string.IsNullOrWhiteSpace(l)));

            var added = newLines.Where(l => !string.IsNullOrWhiteSpace(l) && !oldSet.Contains(l)).Take(8).ToList();
            var removed = oldLines.Where(l => !string.IsNullOrWhiteSpace(l) && !newSet.Contains(l)).Take(8).ToList();

            if (!added.Any() && !removed.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var line in added)
            {
                sb.Append("<div class='rounded bg-emerald-50 px-2 py-1 text-emerald-800'><strong>+ </strong>")
                    .Append(HttpUtility.HtmlEncode(line))
                    .Append("</div>");
            }

            foreach (var line in removed)
            {
                sb.Append("<div class='rounded bg-red-50 px-2 py-1 text-red-800'><strong>- </strong>")
                    .Append(HttpUtility.HtmlEncode(line))
                    .Append("</div>");
            }

            return sb.ToString();
        }
    }
}
