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
        private const string ClarificationPrefix = "CLARIFICATION_REQUEST::";
        private const string QueueApproveAction = "ADMIN_APPROVE_FOR_QUEUE";
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

        public async Task<ActionResult> Dashboard()
        {
            var reviewerId = User.Identity.GetUserId();

            var pendingItems = await BuildPendingReviewQuery(reviewerId)
                .OrderBy(r => r.SubmittedDate)
                .Select(r => new PendingReviewListItemViewModel
                {
                    ContentId = r.ContentId,
                    ReviewId = r.ReviewId,
                    Title = r.Content.Title,
                    Status = r.Status,
                    SubmittedDate = r.SubmittedDate,
                    ChapterCount = r.Content.Chapters.Count(ch => !ch.IsDeleted)
                })
                .ToListAsync();

            var completedReviewCount = await _db.Reviews
                .CountAsync(r => r.ReviewerId == reviewerId && (r.Status == ReviewStatuses.Approved || r.Status == ReviewStatuses.Rejected));

            var clarificationCount = await _db.Reviews
                .CountAsync(r => r.ReviewerId == reviewerId && r.Status == ReviewStatuses.Pending && !string.IsNullOrEmpty(r.AuthorChangeNotes));

            var model = new ReviewDashboardViewModel
            {
                PendingReviewCount = pendingItems.Count,
                CompletedReviewCount = completedReviewCount,
                NotificationCount = pendingItems.Count + clarificationCount,
                OldestPendingSubmitted = pendingItems.Any() ? pendingItems.Min(p => p.SubmittedDate) : (DateTime?)null,
                NextPendingReviews = pendingItems.Take(5).ToList()
            };

            return View(model);
        }

        public async Task<ActionResult> Notifications()
        {
            var reviewerId = User.Identity.GetUserId();

            var assignments = await BuildPendingReviewQuery(reviewerId)
                .Select(r => new ReviewNotificationListItemViewModel
                {
                    ContentId = r.ContentId,
                    ReviewId = r.ReviewId,
                    NotificationType = "Assignment",
                    Title = r.Content.Title,
                    Message = "New draft assigned for review.",
                    CreatedDate = r.SubmittedDate
                })
                .ToListAsync();

            var clarifications = await _db.Reviews
                .Where(r => r.ReviewerId == reviewerId && r.Status == ReviewStatuses.Pending && !string.IsNullOrEmpty(r.AuthorChangeNotes))
                .Select(r => new ReviewNotificationListItemViewModel
                {
                    ContentId = r.ContentId,
                    ReviewId = r.ReviewId,
                    NotificationType = "Author Clarification",
                    Title = r.Content.Title,
                    Message = r.AuthorChangeNotes,
                    CreatedDate = r.SubmittedDate
                })
                .ToListAsync();

            var items = assignments
                .Concat(clarifications)
                .OrderByDescending(n => n.CreatedDate)
                .Take(50)
                .ToList();

            var model = new ReviewNotificationsViewModel
            {
                TotalNotifications = items.Count,
                Items = items
            };

            return View(model);
        }

        public async Task<ActionResult> PendingReviews()
        {
            var reviewerId = User.Identity.GetUserId();

            var pendingItems = await BuildPendingReviewQuery(reviewerId)
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
                .Where(r => r.ContentId == contentId && r.ReviewerId == reviewerId && r.Status == ReviewStatuses.Pending)
                .OrderByDescending(r => r.SubmittedDate)
                .FirstOrDefaultAsync();

            if (review == null)
            {
                TempData["ErrorMessage"] = "No active pending review was found for this content.";
                return RedirectToAction("PendingReviews");
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

            var highlightedChanges = await BuildHighlightedChangesAsync(review.ContentId);
            model.HighlightedChangesHtml = highlightedChanges.IsolatedHtml;
            model.HighlightedChangesIsolatedHtml = highlightedChanges.IsolatedHtml;
            model.HighlightedChangesFullHtml = highlightedChanges.FullHtml;
            model.HasHighlightedChanges = highlightedChanges.HasChanges;
            model.FullPreviewHtml = BuildFullPreviewHtml(review.Content.Description, review.Content.Chapters
                .Where(ch => !ch.IsDeleted)
                .OrderBy(ch => ch.ChapterOrder)
                .Select(ch => new ChapterPreviewItem
                {
                    ChapterTitle = ch.ChapterTitle,
                    ChapterBody = ch.ChapterBody,
                    ChapterOrder = ch.ChapterOrder
                })
                .ToList());

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

        private IQueryable<ReviewEntity> BuildPendingReviewQuery(string reviewerId)
        {
            return _db.Reviews.Where(r =>
                r.ReviewerId == reviewerId &&
                r.Status == ReviewStatuses.Pending &&
                (
                    _db.ContentReviewerAssignments.Any(a => a.ContentId == r.ContentId && a.ReviewerId == r.ReviewerId && a.IsActive) ||
                    !_db.ContentReviewerAssignments.Any(a => a.ContentId == r.ContentId && a.ReviewerId == r.ReviewerId)
                ));
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

            var previousStatus = review.Content.Status;
            review.Content.Status = ContentStatuses.Approved;
            review.Content.LastModifiedDate = DateTime.UtcNow;

            var otherAssignments = await _db.ContentReviewerAssignments
                .Where(a => a.ContentId == model.ContentId && a.ReviewerId != reviewerId && a.IsActive)
                .ToListAsync();
            foreach (var assignment in otherAssignments)
            {
                assignment.IsActive = false;
            }

            await _db.SaveChangesAsync();

            await _audit.LogAsync(reviewerId, AuditActions.Approve, "Review", review.ReviewId, ReviewStatuses.Pending, ReviewStatuses.Approved, Request.UserHostAddress, "Reviewer approved content.");
            await _audit.LogAsync(reviewerId, AuditActions.StatusChange, "Content", model.ContentId, previousStatus, ContentStatuses.Approved, Request.UserHostAddress, "At least one reviewer approved content; moved to admin queue.");
            await _notifications.NotifyContentApprovedAsync(review.Content);
            await _versions.SaveSnapshotAsync(model.ContentId, "APPROVE_CONTENT", reviewerId, "First reviewer approved content.");
            await _versions.SaveSnapshotAsync(model.ContentId, QueueApproveAction, reviewerId, "Automatically moved to Admin queue as Awaiting Preview.");

            TempData["SuccessMessage"] = "Review approved. Content moved to Awaiting Preview in the Admin queue.";
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RequestClarification(ReviewClarificationRequestViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Message))
            {
                TempData["ErrorMessage"] = "Clarification request note is required.";
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
                TempData["ErrorMessage"] = "Archived content cannot receive clarification requests.";
                return RedirectToAction("PendingReviews");
            }

            var sanitizedMessage = HtmlContentSanitizer.StripScripts(model.Message);
            review.Comments = ClarificationPrefix + sanitizedMessage;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(
                userId: reviewerId,
                action: AuditActions.Update,
                entityType: "Review",
                entityId: review.ReviewId,
                oldValue: null,
                newValue: "CLARIFICATION_REQUESTED",
                ipAddress: Request.UserHostAddress,
                changeDetails: "Reviewer requested clarification from author.");

            TempData["SuccessMessage"] = "Clarification requested from author.";
            return RedirectToAction("ReviewContent", new { contentId = model.ContentId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }

            base.Dispose(disposing);
        }

        private async Task<HighlightedChangesResult> BuildHighlightedChangesAsync(Guid contentId)
        {
            var versions = await _db.ContentVersions
                .Where(v => v.ContentId == contentId)
                .OrderByDescending(v => v.VersionNumber)
                .Take(2)
                .ToListAsync();

            if (versions.Count < 2)
            {
                const string noVersionMessage = "<p class='text-slate-500'>No previous version available to highlight changes yet.</p>";
                return new HighlightedChangesResult
                {
                    IsolatedHtml = noVersionMessage,
                    FullHtml = noVersionMessage,
                    HasChanges = false
                };
            }

            var latest = SafeParseSnapshot(versions[0].SnapshotJson);
            var previous = SafeParseSnapshot(versions[1].SnapshotJson);
            if (latest == null || previous == null)
            {
                const string parsingError = "<p class='text-slate-500'>Unable to compute highlighted changes for this submission.</p>";
                return new HighlightedChangesResult
                {
                    IsolatedHtml = parsingError,
                    FullHtml = parsingError,
                    HasChanges = false
                };
            }

            var latestChapters = latest["Chapters"] as JArray ?? new JArray();
            var previousChapters = previous["Chapters"] as JArray ?? new JArray();
            var prevById = previousChapters
                .OfType<JObject>()
                .Where(c => c["ChapterId"] != null)
                .ToDictionary(c => (string)c["ChapterId"], c => c);

            var isolated = new StringBuilder();
            var full = new StringBuilder();
            isolated.Append("<div class='space-y-3'>");
            full.Append("<div class='space-y-3'>");
            var hasChanges = false;

            foreach (var chapter in latestChapters.OfType<JObject>())
            {
                var chapterId = (string)chapter["ChapterId"];
                var chapterTitle = (string)chapter["ChapterTitle"] ?? "Chapter";
                var chapterBody = (string)chapter["ChapterBody"] ?? string.Empty;

                prevById.TryGetValue(chapterId ?? string.Empty, out var previousChapter);
                var previousBody = previousChapter == null ? string.Empty : (string)previousChapter["ChapterBody"] ?? string.Empty;

                var rows = BuildDiffRows(previousBody, chapterBody);
                if (!rows.Any(r => r.Type != "equal"))
                {
                    continue;
                }

                hasChanges = true;

                var isolatedRows = RenderDiffRows(rows, includeUnchanged: false);
                var fullRows = RenderDiffRows(rows, includeUnchanged: true);

                isolated.Append("<section class='rounded border border-slate-200 p-3'>")
                    .Append("<h4 class='mb-2 font-semibold text-slate-900'>Section: ")
                    .Append(HttpUtility.HtmlEncode(chapterTitle))
                    .Append("</h4>")
                    .Append("<div class='space-y-1 text-sm'>")
                    .Append(isolatedRows)
                    .Append("</div></section>");

                full.Append("<section class='rounded border border-slate-200 p-3'>")
                    .Append("<h4 class='mb-2 font-semibold text-slate-900'>Section: ")
                    .Append(HttpUtility.HtmlEncode(chapterTitle))
                    .Append("</h4>")
                    .Append("<div class='space-y-1 text-sm'>")
                    .Append(fullRows)
                    .Append("</div></section>");
            }

            isolated.Append("</div>");
            full.Append("</div>");

            if (!hasChanges)
            {
                const string noChanges = "<p class='text-slate-500'>No edited sections were detected between the latest two versions.</p>";
                return new HighlightedChangesResult
                {
                    IsolatedHtml = noChanges,
                    FullHtml = noChanges,
                    HasChanges = false
                };
            }

            return new HighlightedChangesResult
            {
                IsolatedHtml = isolated.ToString(),
                FullHtml = full.ToString(),
                HasChanges = true
            };
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

        private static List<DiffRow> BuildDiffRows(string oldText, string newText)
        {
            var oldLines = (oldText ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var newLines = (newText ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var lcs = new int[oldLines.Length + 1, newLines.Length + 1];

            for (var i = oldLines.Length - 1; i >= 0; i--)
            {
                for (var j = newLines.Length - 1; j >= 0; j--)
                {
                    if (string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
                    {
                        lcs[i, j] = lcs[i + 1, j + 1] + 1;
                    }
                    else
                    {
                        lcs[i, j] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                    }
                }
            }

            var rawOps = new List<DiffRow>();
            var oldIndex = 0;
            var newIndex = 0;
            while (oldIndex < oldLines.Length && newIndex < newLines.Length)
            {
                if (string.Equals(oldLines[oldIndex], newLines[newIndex], StringComparison.Ordinal))
                {
                    rawOps.Add(new DiffRow { Type = "equal", Line = oldLines[oldIndex] });
                    oldIndex++;
                    newIndex++;
                }
                else if (lcs[oldIndex + 1, newIndex] >= lcs[oldIndex, newIndex + 1])
                {
                    rawOps.Add(new DiffRow { Type = "remove", Line = oldLines[oldIndex] });
                    oldIndex++;
                }
                else
                {
                    rawOps.Add(new DiffRow { Type = "add", Line = newLines[newIndex] });
                    newIndex++;
                }
            }

            while (oldIndex < oldLines.Length)
            {
                rawOps.Add(new DiffRow { Type = "remove", Line = oldLines[oldIndex++] });
            }

            while (newIndex < newLines.Length)
            {
                rawOps.Add(new DiffRow { Type = "add", Line = newLines[newIndex++] });
            }

            var normalized = new List<DiffRow>();
            for (var i = 0; i < rawOps.Count; i++)
            {
                if (rawOps[i].Type != "remove")
                {
                    normalized.Add(rawOps[i]);
                    continue;
                }

                var removedBlock = new List<string>();
                var addedBlock = new List<string>();
                var cursor = i;

                while (cursor < rawOps.Count && rawOps[cursor].Type == "remove")
                {
                    removedBlock.Add(rawOps[cursor].Line);
                    cursor++;
                }

                while (cursor < rawOps.Count && rawOps[cursor].Type == "add")
                {
                    addedBlock.Add(rawOps[cursor].Line);
                    cursor++;
                }

                if (addedBlock.Count == 0)
                {
                    foreach (var removed in removedBlock)
                    {
                        normalized.Add(new DiffRow { Type = "remove", Line = removed });
                    }
                }
                else
                {
                    var pairCount = Math.Min(removedBlock.Count, addedBlock.Count);
                    for (var pair = 0; pair < pairCount; pair++)
                    {
                        normalized.Add(new DiffRow
                        {
                            Type = "modify",
                            OldLine = removedBlock[pair],
                            NewLine = addedBlock[pair]
                        });
                    }

                    for (var rem = pairCount; rem < removedBlock.Count; rem++)
                    {
                        normalized.Add(new DiffRow { Type = "remove", Line = removedBlock[rem] });
                    }

                    for (var add = pairCount; add < addedBlock.Count; add++)
                    {
                        normalized.Add(new DiffRow { Type = "add", Line = addedBlock[add] });
                    }
                }

                i = cursor - 1;
            }

            return normalized;
        }

        private static string RenderDiffRows(IEnumerable<DiffRow> rows, bool includeUnchanged)
        {
            var sb = new StringBuilder();

            foreach (var row in rows)
            {
                if (row.Type == "equal")
                {
                    if (!includeUnchanged || string.IsNullOrWhiteSpace(row.Line))
                    {
                        continue;
                    }

                    sb.Append("<div class='rounded bg-slate-50 px-2 py-1 text-slate-600'>")
                        .Append(HttpUtility.HtmlEncode(row.Line))
                        .Append("</div>");
                    continue;
                }

                if (row.Type == "add")
                {
                    sb.Append("<div class='rounded bg-emerald-50 px-2 py-1 text-emerald-800'>")
                        .Append("<span class='font-semibold'>+ </span>")
                        .Append(HttpUtility.HtmlEncode(row.Line))
                        .Append("</div>");
                    continue;
                }

                if (row.Type == "remove")
                {
                    sb.Append("<div class='rounded bg-red-50 px-2 py-1 text-red-800'>")
                        .Append("<span class='font-semibold'>- </span>")
                        .Append(HttpUtility.HtmlEncode(row.Line))
                        .Append("</div>");
                    continue;
                }

                if (row.Type == "modify")
                {
                    sb.Append("<div class='rounded bg-amber-50 px-2 py-1 text-amber-900'>")
                        .Append(HttpUtility.HtmlEncode(row.NewLine ?? string.Empty))
                        .Append("</div>");
                }
            }

            return sb.ToString();
        }

        private static string BuildFullPreviewHtml(string description, IList<ChapterPreviewItem> chapters)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(description))
            {
                sb.Append("<section class='prose max-w-none text-slate-700'>")
                    .Append(description)
                    .Append("</section>");
            }

            foreach (var chapter in chapters)
            {
                sb.Append("<section class='mt-6 border-t border-slate-100 pt-4'>")
                    .Append("<h4 class='text-lg font-semibold text-slate-900'>")
                    .Append(HttpUtility.HtmlEncode(chapter.ChapterOrder + ". " + (chapter.ChapterTitle ?? "Chapter")))
                    .Append("</h4>")
                    .Append("<div class='prose mt-2 max-w-none text-slate-700'>")
                    .Append(chapter.ChapterBody ?? string.Empty)
                    .Append("</div>")
                    .Append("</section>");
            }

            return sb.ToString();
        }

        private sealed class HighlightedChangesResult
        {
            public string IsolatedHtml { get; set; }
            public string FullHtml { get; set; }
            public bool HasChanges { get; set; }
        }

        private sealed class DiffRow
        {
            public string Type { get; set; }
            public string Line { get; set; }
            public string OldLine { get; set; }
            public string NewLine { get; set; }
        }

        private sealed class ChapterPreviewItem
        {
            public string ChapterTitle { get; set; }
            public string ChapterBody { get; set; }
            public int ChapterOrder { get; set; }
        }
    }
}
