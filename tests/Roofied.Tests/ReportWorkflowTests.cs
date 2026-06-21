using Roofied.Application.Reports;
using Roofied.Domain.Enums;

namespace Roofied.Tests;

public class ReportWorkflowTests
{
    [Theory]
    [InlineData(ReportStatus.Draft, ReportStatus.PendingReview, true)]
    [InlineData(ReportStatus.PendingReview, ReportStatus.Approved, true)]
    [InlineData(ReportStatus.PendingReview, ReportStatus.Rejected, true)]
    [InlineData(ReportStatus.PendingReview, ReportStatus.NeedsClarification, true)]
    [InlineData(ReportStatus.NeedsClarification, ReportStatus.PendingReview, true)]
    [InlineData(ReportStatus.Approved, ReportStatus.Archived, true)]
    [InlineData(ReportStatus.Rejected, ReportStatus.PendingReview, true)]
    public void Allowed_transitions(ReportStatus from, ReportStatus to, bool expected) =>
        Assert.Equal(expected, ReportWorkflow.CanTransition(from, to));

    [Theory]
    [InlineData(ReportStatus.Draft, ReportStatus.Approved)]      // must go through review
    [InlineData(ReportStatus.Archived, ReportStatus.Approved)]   // archived is terminal
    [InlineData(ReportStatus.Archived, ReportStatus.PendingReview)]
    [InlineData(ReportStatus.Rejected, ReportStatus.Approved)]   // must reopen to pending first
    public void Disallowed_transitions(ReportStatus from, ReportStatus to) =>
        Assert.False(ReportWorkflow.CanTransition(from, to));

    [Fact]
    public void Only_approved_is_publishable()
    {
        Assert.True(ReportWorkflow.IsPublishable(ReportStatus.Approved));
        foreach (var s in Enum.GetValues<ReportStatus>().Where(s => s != ReportStatus.Approved))
            Assert.False(ReportWorkflow.IsPublishable(s));
    }
}
