using System;
using ContentPublishing.Application.Rules;
using Xunit;

namespace ContentPublishing.UnitTests
{
    public class ContentWorkflowRulesTests
    {
        [Fact]
        public void CanSubmitForReview_Requires_AtLeastOneChapter_AndEditableStatus()
        {
            Assert.False(ContentWorkflowRules.CanSubmitForReview("Draft", 0));
            Assert.True(ContentWorkflowRules.CanSubmitForReview("Draft", 1));
            Assert.False(ContentWorkflowRules.CanSubmitForReview("Archived", 3));
        }

        [Fact]
        public void CanPublish_Only_Allows_Approved_Status()
        {
            Assert.True(ContentWorkflowRules.CanPublish("Approved"));
            Assert.False(ContentWorkflowRules.CanPublish("Draft"));
            Assert.False(ContentWorkflowRules.CanPublish("UnderReview"));
        }

        [Fact]
        public void ShouldAutoPublish_Requires_Approved_And_DueSchedule()
        {
            var now = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

            Assert.True(ContentWorkflowRules.ShouldAutoPublish("Approved", now.AddMinutes(-1), now));
            Assert.False(ContentWorkflowRules.ShouldAutoPublish("Approved", now.AddMinutes(1), now));
            Assert.False(ContentWorkflowRules.ShouldAutoPublish("Draft", now.AddMinutes(-1), now));
            Assert.False(ContentWorkflowRules.ShouldAutoPublish("Approved", null, now));
        }

        [Fact]
        public void ResolveStatusAfterApproval_Only_PublishesApproval_When_NoPendingRemain()
        {
            Assert.Equal("Approved", ContentWorkflowRules.ResolveStatusAfterApproval("UnderReview", 0));
            Assert.Equal("UnderReview", ContentWorkflowRules.ResolveStatusAfterApproval("UnderReview", 1));
        }

        [Fact]
        public void ResolveStatusAfterRejection_Returns_Draft()
        {
            Assert.Equal("Draft", ContentWorkflowRules.ResolveStatusAfterRejection());
        }
    }
}