using ContentPublishing.Application.Rules;
using ContentPublishing.Web.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ContentPublishing.IntegrationTests
{
    [TestClass]
    public class WorkflowSmokeTests
    {
        [TestMethod]
        public void ApprovedContentModel_AndWorkflowRules_AreConsistentAcrossAssemblies()
        {
            var content = new ContentEntity
            {
                Status = ContentStatuses.Approved
            };

            var canPublish = ContentWorkflowRules.CanPublish(content.Status);

            Assert.IsTrue(canPublish);
        }

        [TestMethod]
        public void ArchivedContentModel_AndWorkflowRules_BlockEditingAcrossAssemblies()
        {
            var content = new ContentEntity
            {
                Status = ContentStatuses.Archived
            };

            var canEdit = ContentWorkflowRules.CanEdit(content.Status);

            Assert.IsFalse(canEdit);
        }
    }
}
