using System;
using System.Threading.Tasks;

namespace ContentPublishing.Application.Interfaces
{
    public interface IContentWorkflowService
    {
        Task SubmitForReviewAsync(Guid contentId, Guid userId, string ipAddress);
        Task ApproveAsync(Guid contentId, Guid reviewerId, string comments, string ipAddress);
        Task RejectAsync(Guid contentId, Guid reviewerId, string comments, string ipAddress);
        Task PublishAsync(Guid contentId, Guid adminId, DateTime? publishAtUtc, string ipAddress);
        Task ArchiveAsync(Guid contentId, Guid adminId, string ipAddress);
    }
}
