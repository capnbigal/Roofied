using Roofied.Domain.Enums;

namespace Roofied.Application.Reports;

/// <summary>
/// Pure state machine for report status transitions. Centralizes the allowed moderation workflow
/// so the rules can be unit-tested independently of persistence and UI.
///
/// Allowed transitions:
///   Draft              -> PendingReview
///   PendingReview      -> Approved | Rejected | NeedsClarification | Archived
///   NeedsClarification -> PendingReview | Rejected | Archived
///   Approved           -> Archived | NeedsClarification (unpublish) | Rejected
///   Rejected           -> Archived | PendingReview (reopen)
///   Archived           -> (terminal)
/// </summary>
public static class ReportWorkflow
{
    private static readonly Dictionary<ReportStatus, ReportStatus[]> Allowed = new()
    {
        [ReportStatus.Draft] = new[] { ReportStatus.PendingReview },
        [ReportStatus.PendingReview] = new[]
        {
            ReportStatus.Approved, ReportStatus.Rejected, ReportStatus.NeedsClarification, ReportStatus.Archived,
        },
        [ReportStatus.NeedsClarification] = new[]
        {
            ReportStatus.PendingReview, ReportStatus.Rejected, ReportStatus.Archived,
        },
        [ReportStatus.Approved] = new[]
        {
            ReportStatus.Archived, ReportStatus.NeedsClarification, ReportStatus.Rejected,
        },
        [ReportStatus.Rejected] = new[]
        {
            ReportStatus.Archived, ReportStatus.PendingReview,
        },
        [ReportStatus.Archived] = Array.Empty<ReportStatus>(),
    };

    public static bool CanTransition(ReportStatus from, ReportStatus to) =>
        from == to || (Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0);

    public static IReadOnlyList<ReportStatus> AllowedTransitions(ReportStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : Array.Empty<ReportStatus>();

    /// <summary>True if a report in this status is eligible for public surfaces.</summary>
    public static bool IsPublishable(ReportStatus status) => status == ReportStatus.Approved;
}
