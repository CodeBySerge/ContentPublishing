using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Web;
using ContentPublishing.Web.Models;
using Microsoft.AspNet.Identity;

namespace ContentPublishing.Web.Services
{
    public class WorkflowNotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IIdentityMessageService _emailService;
        private readonly string _appBaseUrl;

        public WorkflowNotificationService(ApplicationDbContext db, IIdentityMessageService emailService)
        {
            _db = db;
            _emailService = emailService;
            _appBaseUrl = ConfigurationManager.AppSettings["appBaseUrl"] ?? "http://localhost:8080";
        }

        public async Task NotifyContentSubmittedAsync(ContentEntity content, IEnumerable<string> reviewerIds, string changeNotes)
        {
            var reviewers = await LoadUsersAsync(reviewerIds);
            var notesHtml = string.IsNullOrWhiteSpace(changeNotes)
                ? "<p><strong>Author change notes:</strong> None provided.</p>"
                : "<p><strong>Author change notes:</strong></p><p>" + HttpUtility.HtmlEncode(changeNotes).Replace("\n", "<br />") + "</p>";

            foreach (var reviewer in reviewers)
            {
                await _emailService.SendAsync(new IdentityMessage
                {
                    Destination = reviewer.Email,
                    Subject = "New content submitted for review",
                    Body = $"<p>{content.Title} has been submitted for your review.</p>{notesHtml}<p><a href=\"{_appBaseUrl}/Review/ReviewContent?contentId={content.ContentId}\">Open review</a></p>"
                });
            }

            var author = await _db.Users.FirstOrDefaultAsync(u => u.Id == content.AuthorId);
            if (author != null)
            {
                await _emailService.SendAsync(new IdentityMessage
                {
                    Destination = author.Email,
                    Subject = "Your content was submitted for review",
                    Body = $"<p>Your content <strong>{content.Title}</strong> has been submitted for review.</p>"
                });
            }
        }

        public async Task NotifyReviewerAssignedAsync(ContentEntity content, string reviewerId)
        {
            var reviewer = await _db.Users.FirstOrDefaultAsync(u => u.Id == reviewerId);
            if (reviewer == null)
            {
                return;
            }

            await _emailService.SendAsync(new IdentityMessage
            {
                Destination = reviewer.Email,
                Subject = "You have been assigned a review",
                Body = $"<p>You have been assigned to review <strong>{content.Title}</strong>.</p><p><a href=\"{_appBaseUrl}/Review/ReviewContent?contentId={content.ContentId}\">Open review</a></p>"
            });
        }

        public async Task NotifyContentApprovedAsync(ContentEntity content)
        {
            var author = await _db.Users.FirstOrDefaultAsync(u => u.Id == content.AuthorId);
            if (author == null)
            {
                return;
            }

            await _emailService.SendAsync(new IdentityMessage
            {
                Destination = author.Email,
                Subject = "Your content has been approved",
                Body = $"<p>Your content <strong>{content.Title}</strong> has been approved and is ready for publishing.</p>"
            });
        }

        public async Task NotifyContentRejectedAsync(ContentEntity content, string comments)
        {
            var author = await _db.Users.FirstOrDefaultAsync(u => u.Id == content.AuthorId);
            if (author == null)
            {
                return;
            }

            await _emailService.SendAsync(new IdentityMessage
            {
                Destination = author.Email,
                Subject = "Your content has been rejected",
                Body = $"<p>Your content <strong>{content.Title}</strong> was rejected and returned to draft.</p><p>Comments:</p><p>{comments}</p>"
            });
        }

        public async Task NotifyContentPublishedAsync(ContentEntity content)
        {
            var author = await _db.Users.FirstOrDefaultAsync(u => u.Id == content.AuthorId);
            if (author == null)
            {
                return;
            }

            await _emailService.SendAsync(new IdentityMessage
            {
                Destination = author.Email,
                Subject = "Your content has been published",
                Body = $"<p>Your content <strong>{content.Title}</strong> has been published.</p>"
            });
        }

        private async Task<List<ApplicationUser>> LoadUsersAsync(IEnumerable<string> userIds)
        {
            var ids = userIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList() ?? new List<string>();
            return await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
        }
    }
}
