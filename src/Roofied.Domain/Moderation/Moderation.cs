using Roofied.Domain.Channels;
using Roofied.Domain.Common;
using Roofied.Domain.Enums;
using Roofied.Domain.Reports;

namespace Roofied.Domain.Moderation;

/// <summary>
/// A moderation case groups review activity for a single piece of content (a report or a channel post).
/// Exactly one of <see cref="ReportId"/> / <see cref="ChannelPostId"/> is set.
/// </summary>
public class ModerationCase : FullAuditableEntity
{
    public Guid? ReportId { get; set; }
    public Report? Report { get; set; }

    public Guid? ChannelPostId { get; set; }
    public ChannelPost? ChannelPost { get; set; }

    public ModerationCaseState State { get; set; } = ModerationCaseState.Open;
    public ModerationPriority Priority { get; set; } = ModerationPriority.Normal;

    /// <summary>Moderator the case is assigned to, if any.</summary>
    public string? AssignedToUserId { get; set; }

    public DateTime? ResolvedUtc { get; set; }
    public string? ResolvedByUserId { get; set; }

    public ICollection<ModerationNote> Notes { get; set; } = new List<ModerationNote>();
}

/// <summary>Internal, moderator-only note attached to a moderation case. Never public.</summary>
public class ModerationNote : AuditableEntity
{
    public Guid ModerationCaseId { get; set; }
    public ModerationCase? ModerationCase { get; set; }

    public required string AuthorUserId { get; set; }
    public required string Text { get; set; }
}

/// <summary>
/// A user-submitted flag against a piece of content. Drives the moderation queue.
/// </summary>
public class ContentFlag : FullAuditableEntity
{
    public ModeratedContentType ContentType { get; set; }
    public Guid ContentId { get; set; }

    public FlagReason Reason { get; set; } = FlagReason.Other;
    public string? Details { get; set; }

    /// <summary>Null for anonymous flags.</summary>
    public string? FlaggedByUserId { get; set; }

    /// <summary>Salted hash of reporter IP for abuse correlation (never the raw IP).</summary>
    public string? ReporterIpHash { get; set; }

    public FlagStatus Status { get; set; } = FlagStatus.Open;
    public string? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedUtc { get; set; }
}
