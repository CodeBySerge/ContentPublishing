using System;
using System.Threading.Tasks;
using ContentPublishing.Web.Models;

namespace ContentPublishing.Web.Services
{
    public class AuditLogService
    {
        private readonly ApplicationDbContext _db;

        public AuditLogService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(string userId, string action, string entityType, Guid entityId, string oldValue, string newValue, string ipAddress, string changeDetails)
        {
            _db.AuditLogs.Add(new AuditLogEntity
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue,
                NewValue = newValue,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                ChangeDetails = changeDetails
            });

            await _db.SaveChangesAsync();
        }
    }
}
