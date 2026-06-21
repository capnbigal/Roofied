using Roofied.Application.Common;
using Roofied.Domain.Enums;

namespace Roofied.Application.Moderation;

public interface IModerationService
{
    // --- Report moderation ---
    Task<PagedResult<ModeratorReportListItemDto>> GetReportQueueAsync(
        ReportStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task<ModeratorReportDetailDto?> GetReportForModerationAsync(Guid reportId, CancellationToken ct = default);

    Task<OperationResult> ApproveReportAsync(ApproveReportInput input, CancellationToken ct = default);
    Task<OperationResult> RejectReportAsync(Guid reportId, string reason, CancellationToken ct = default);
    Task<OperationResult> RequestClarificationAsync(Guid reportId, string note, CancellationToken ct = default);
    Task<OperationResult> ArchiveReportAsync(Guid reportId, string reason, CancellationToken ct = default);

    /// <summary>Stores a moderator-redacted narrative working copy (does not auto-publish).</summary>
    Task<OperationResult> RedactNarrativeAsync(Guid reportId, string redactedNarrative, CancellationToken ct = default);

    /// <summary>Recomputes the public (fuzzed) location using a new grid precision.</summary>
    Task<OperationResult> AdjustPrecisionAsync(Guid reportId, int gridSizeMeters, CancellationToken ct = default);

    Task<OperationResult> AddNoteAsync(Guid reportId, string text, CancellationToken ct = default);

    // --- Channel post moderation ---
    Task<PagedResult<ChannelPostModerationDto>> GetPostQueueAsync(
        ChannelPostStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<OperationResult> ApprovePostAsync(Guid postId, CancellationToken ct = default);
    Task<OperationResult> RejectPostAsync(Guid postId, string reason, CancellationToken ct = default);
    Task<OperationResult> HidePostAsync(Guid postId, CancellationToken ct = default);
    Task<OperationResult> SetPostPinnedAsync(Guid postId, bool pinned, CancellationToken ct = default);
    Task<OperationResult> SetPostLockedAsync(Guid postId, bool locked, CancellationToken ct = default);
    Task<OperationResult> RedactPostAsync(Guid postId, string redactedBody, CancellationToken ct = default);

    // --- Flags ---
    Task<PagedResult<ContentFlagDto>> GetFlagsAsync(FlagStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<OperationResult> ResolveFlagAsync(Guid flagId, string resolutionNote, CancellationToken ct = default);
    Task<OperationResult> DismissFlagAsync(Guid flagId, string reason, CancellationToken ct = default);
}

public sealed record ChannelPostModerationDto
{
    public required Guid Id { get; init; }
    public required string ChannelName { get; init; }
    public required string Title { get; init; }
    public string? Body { get; init; }
    public string? AuthorDisplayName { get; init; }
    public ChannelPostStatus Status { get; init; }
    public bool IsPinned { get; init; }
    public bool IsLocked { get; init; }
    public bool IsHidden { get; init; }
    public int OpenFlagCount { get; init; }
    public DateTime CreatedUtc { get; init; }
}
