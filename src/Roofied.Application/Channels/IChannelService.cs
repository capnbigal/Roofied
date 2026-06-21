using Roofied.Application.Common;
using Roofied.Domain.Enums;

namespace Roofied.Application.Channels;

public sealed record ChannelDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? Guidelines,
    bool IsLocked,
    bool AllowAnonymousPosts,
    bool CommentsEnabled,
    int ApprovedPostCount);

public sealed record ChannelPostDto
{
    public required Guid Id { get; init; }
    public required string ChannelSlug { get; init; }
    public required string ChannelName { get; init; }
    public required string Title { get; init; }
    /// <summary>Public body — the redacted body when present, otherwise the approved body.</summary>
    public required string Body { get; init; }
    public string? AuthorDisplayName { get; init; }
    public bool IsPinned { get; init; }
    public bool IsLocked { get; init; }
    public DateTime? PublishedUtc { get; init; }
}

public sealed class ChannelPostInput
{
    public Guid ChannelId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool PostAnonymously { get; set; } = true;
    public bool GuidelinesAcknowledged { get; set; }
    public string? CaptchaToken { get; set; }
}

public interface IChannelService
{
    Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(CancellationToken ct = default);
    Task<ChannelDto?> GetChannelBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Approved, non-hidden posts for a channel, newest-first, paginated, optional search.</summary>
    Task<PagedResult<ChannelPostDto>> GetPostsAsync(
        string channelSlug, string? search, int page, int pageSize, CancellationToken ct = default);

    Task<ChannelPostDto?> GetPostAsync(Guid postId, CancellationToken ct = default);

    /// <summary>Creates a post. Held in PendingReview; applies sanitization, PII flagging, rate limiting.</summary>
    Task<OperationResult<Guid>> CreatePostAsync(ChannelPostInput input, CancellationToken ct = default);

    Task<IReadOnlyList<ChannelPostDto>> GetMyPostsAsync(string userId, CancellationToken ct = default);
}
