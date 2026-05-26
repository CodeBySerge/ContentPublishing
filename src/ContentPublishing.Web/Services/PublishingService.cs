using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using ContentPublishing.Application.Rules;
using ContentPublishing.Web.Models;

namespace ContentPublishing.Web.Services
{
    public class PublishingService
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditLogService _audit;
        private readonly ContentVersionService _versions;
        private readonly WorkflowNotificationService _notifications;

        public PublishingService(ApplicationDbContext db)
        {
            _db = db;
            _audit = new AuditLogService(db);
            _versions = new ContentVersionService(db);
            _notifications = new WorkflowNotificationService(db, new SmtpEmailService());
        }

        public async Task<int> PublishDueContentAsync(string ipAddress)
        {
            var now = DateTime.UtcNow;
            var dueItems = await _db.Contents
                .Where(c => c.Status == ContentStatuses.Approved && c.ScheduledPublishDate.HasValue)
                .ToListAsync();

            dueItems = dueItems
                .Where(c => ContentWorkflowRules.ShouldAutoPublish(c.Status, c.ScheduledPublishDate, now))
                .ToList();

            foreach (var content in dueItems)
            {
                content.Status = ContentStatuses.Published;
                content.PublishedDate = now;
                content.LastModifiedDate = now;
                content.ScheduledPublishDate = null;
            }

            if (!dueItems.Any())
            {
                return 0;
            }

            await _db.SaveChangesAsync();

            foreach (var content in dueItems)
            {
                await _audit.LogAsync(null, AuditActions.Publish, "Content", content.ContentId, ContentStatuses.Approved, ContentStatuses.Published, ipAddress, "Scheduled publish executed automatically.");
                await _versions.SaveSnapshotAsync(content.ContentId, "AUTO_PUBLISH_CONTENT", null, "Scheduled publish executed.");
                await _notifications.NotifyContentPublishedAsync(content);
            }

            return dueItems.Count;
        }
    }
}
