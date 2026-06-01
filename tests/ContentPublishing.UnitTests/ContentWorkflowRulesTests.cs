using System;
using ContentPublishing.Application.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ContentPublishing.UnitTests
{
    [TestClass]
    public class ContentWorkflowRulesTests
    {
        [TestMethod]
        public void CanApprove_WhenStatusUnderReview_ReturnsTrue()
        {
            var result = ContentWorkflowRules.CanApprove("UnderReview");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CanApprove_WhenStatusDraft_ReturnsFalse()
        {
            var result = ContentWorkflowRules.CanApprove("Draft");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CanSubmitForReview_WithAtLeastOneChapterAndEditableStatus_ReturnsTrue()
        {
            var result = ContentWorkflowRules.CanSubmitForReview("Draft", 1);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ResolveStatusAfterApproval_WhenNoPendingReviews_ReturnsApproved()
        {
            var result = ContentWorkflowRules.ResolveStatusAfterApproval("UnderReview", 0);

            Assert.AreEqual("Approved", result);
        }

        [TestMethod]
        public void ShouldAutoPublish_WhenApprovedAndScheduleIsDue_ReturnsTrue()
        {
            var now = DateTime.UtcNow;
            var schedule = now.AddMinutes(-1);

            var result = ContentWorkflowRules.ShouldAutoPublish("Approved", schedule, now);

            Assert.IsTrue(result);
        }
    }
}
