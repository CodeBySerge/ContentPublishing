using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using ContentPublishing.Web.Models;
using Newtonsoft.Json;

namespace ContentPublishing.Web.Services
{
    public class ContentVersionService
    {
        private readonly ApplicationDbContext _db;

        public ContentVersionService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task SaveSnapshotAsync(Guid contentId, string action, string createdByUserId, string notes = null)
        {
            var content = await _db.Contents
                .Include(c => c.Chapters)
                .Include(c => c.Images)
                .SingleOrDefaultAsync(c => c.ContentId == contentId);

            if (content == null)
            {
                return;
            }

            var versionNumber = (await _db.ContentVersions
                .Where(v => v.ContentId == contentId)
                .Select(v => (int?)v.VersionNumber)
                .MaxAsync() ?? 0) + 1;

            var snapshot = new
            {
                content.ContentId,
                content.Title,
                content.Description,
                content.Status,
                content.AuthorId,
                content.CreatedDate,
                content.LastModifiedDate,
                content.PublishedDate,
                content.ArchivedDate,
                Chapters = content.Chapters
                    .OrderBy(ch => ch.ChapterOrder)
                    .Select(ch => new
                    {
                        ch.ChapterId,
                        ch.ChapterTitle,
                        ch.ChapterBody,
                        ch.ChapterOrder,
                        ch.IsDeleted,
                        ch.CreatedDate,
                        ch.LastModifiedDate
                    })
                    .ToList(),
                Images = content.Images
                    .OrderByDescending(img => img.CreatedDate)
                    .Select(img => new
                    {
                        img.ImageId,
                        img.FileName,
                        img.RelativePath,
                        img.ContentType,
                        img.CropX,
                        img.CropY,
                        img.CropWidth,
                        img.CropHeight,
                        img.IsPrimary,
                        img.CreatedDate
                    })
                    .ToList()
            };

            _db.ContentVersions.Add(new ContentVersionEntity
            {
                VersionId = Guid.NewGuid(),
                ContentId = contentId,
                VersionNumber = versionNumber,
                Action = action,
                CreatedByUserId = createdByUserId,
                CreatedDate = DateTime.UtcNow,
                SnapshotJson = JsonConvert.SerializeObject(snapshot, Formatting.Indented),
                Notes = notes
            });

            await _db.SaveChangesAsync();
        }
    }
}
