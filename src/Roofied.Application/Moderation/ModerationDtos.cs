using Roofied.Domain.Enums;

namespace Roofied.Application.Moderation;

public sealed record ModeratorReportListItemDto
{
    public required Guid Id { get; init; }
    public required string ReferenceCode { get; init; }
    public ReportStatus Status { get; init; }
    public ReportVisibility Visibility { get; init; }
    public string? City { get; init; }
    public string? IncidentTypeName { get; init; }
    public DateOnly IncidentDateFrom { get; init; }
    public DateTime CreatedUtc { get; init; }
    public int AutoFlagCount { get; init; }
    public int OpenFlagCount { get; init; }
}

public sealed record ReportStatusHistoryDto(
    ReportStatus? FromStatus,
    ReportStatus ToStatus,
    string? ChangedByUserId,
    string? Reason,
    DateTime CreatedUtc);

public sealed record ModerationNoteDto(string AuthorUserId, string Text, DateTime CreatedUtc);

public sealed record ContentFlagDto(
    Guid Id,
    ModeratedContentType ContentType,
    Guid ContentId,
    FlagReason Reason,
    string? Details,
    FlagStatus Status,
    DateTime CreatedUtc);

/// <summary>
/// FULL moderator view of a report, INCLUDING restricted fields (raw narrative, exact location,
/// private contact, internal notes). Only returned by the moderation service to authorized
/// moderators. Must never be sent to a public surface.
/// </summary>
public sealed record ModeratorReportDetailDto
{
    public required Guid Id { get; init; }
    public required string ReferenceCode { get; init; }
    public ReportStatus Status { get; init; }
    public ReportVisibility Visibility { get; init; }
    public SuspicionLevel SuspicionLevel { get; init; }
    public string? IncidentTypeName { get; init; }
    public string? VenueCategoryName { get; init; }
    public Guid? ReportCategoryId { get; init; }
    public Guid? VenueCategoryId { get; init; }
    public DateOnly IncidentDateFrom { get; init; }
    public DateOnly IncidentDateTo { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? Country { get; init; }
    public bool? MedicalCareSought { get; init; }
    public bool? PoliceReportFiled { get; init; }
    public string? PublicSummary { get; init; }

    // --- Restricted ---
    public string? RawNarrative { get; init; }
    public string? RedactedNarrative { get; init; }
    public string? SymptomsDescription { get; init; }
    public DateTime? ExactIncidentUtc { get; init; }
    public string? PrivateContactMethod { get; init; }
    public string? PrivateContactValue { get; init; }
    public double? ExactLatitude { get; init; }
    public double? ExactLongitude { get; init; }
    public string? ExactAddress { get; init; }
    public IReadOnlyList<string> AutoFlags { get; init; } = Array.Empty<string>();

    // --- Public location preview ---
    public double? ApproxLatitude { get; init; }
    public double? ApproxLongitude { get; init; }
    public int? PrecisionMeters { get; init; }
    public string? GeneralizedAreaLabel { get; init; }

    public IReadOnlyList<string> SafetyTags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ReportStatusHistoryDto> StatusHistory { get; init; } = Array.Empty<ReportStatusHistoryDto>();
    public IReadOnlyList<ModerationNoteDto> Notes { get; init; } = Array.Empty<ModerationNoteDto>();
    public IReadOnlyList<ContentFlagDto> Flags { get; init; } = Array.Empty<ContentFlagDto>();
}

/// <summary>Inputs a moderator provides when approving and publishing a report.</summary>
public sealed class ApproveReportInput
{
    public Guid ReportId { get; set; }
    public string? PublicSummary { get; set; }
    public int? PrecisionMetersOverride { get; set; }
    public string? GeneralizedAreaLabel { get; set; }
    public List<string> SafetyTags { get; set; } = new();
}
