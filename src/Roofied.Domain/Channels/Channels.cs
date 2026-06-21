using Roofied.Domain.Common;
using Roofied.Domain.Enums;

namespace Roofied.Domain.Channels;

/// <summary>A moderated community discussion channel.</summary>
public class Channel : FullAuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }

    /// <summary>Posting guidelines shown prominently before a user creates a post.</summary>
    public string? Guidelines { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>When locked, no new posts may be created.</summary>
    public bool IsLocked { get; set; }

    public bool AllowAnonymousPosts { get; set; } = true;

    /// <summary>Comments are disabled until a moderation policy is implemented (per product rules).</summary>
    public bool CommentsEnabled { get; set; }

    public ICollection<ChannelPost> Posts { get; set; } = new List<ChannelPost>();
}

/// <summary>A post within a channel. Starts in <see cref="ChannelPostStatus.PendingReview"/>.</summary>
public class ChannelPost : FullAuditableEntity
{
    public Guid ChannelId { get; set; }
    public Channel? Channel { get; set; }

    public required string Title { get; set; }

    /// <summary>Sanitized author body.</summary>
    public required string Body { get; set; }

    /// <summary>Moderator-redacted body that replaces <see cref="Body"/> publicly when set.</summary>
    public string? RedactedBody { get; set; }

    /// <summary>Null for anonymous posts.</summary>
    public string? AuthorUserId { get; set; }

    /// <summary>Snapshot of the display name at post time (optional). Never an email.</summary>
    public string? AuthorDisplayName { get; set; }

    /// <summary>Salted hash of author IP for abuse correlation (never the raw IP).</summary>
    public string? AuthorIpHash { get; set; }

    public ChannelPostStatus Status { get; set; } = ChannelPostStatus.PendingReview;
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public bool IsHidden { get; set; }

    public string? ModeratedByUserId { get; set; }
    public DateTime? PublishedUtc { get; set; }

    public ICollection<ChannelComment> Comments { get; set; } = new List<ChannelComment>();
}

/// <summary>
/// A comment on a post. Schema exists, but comments stay disabled until a moderation policy is live
/// (gated by <see cref="Channel.CommentsEnabled"/>).
/// </summary>
public class ChannelComment : FullAuditableEntity
{
    public Guid ChannelPostId { get; set; }
    public ChannelPost? ChannelPost { get; set; }

    public required string Body { get; set; }
    public string? RedactedBody { get; set; }

    public string? AuthorUserId { get; set; }
    public string? AuthorDisplayName { get; set; }
    public string? AuthorIpHash { get; set; }

    public ChannelPostStatus Status { get; set; } = ChannelPostStatus.PendingReview;
    public bool IsHidden { get; set; }
}
