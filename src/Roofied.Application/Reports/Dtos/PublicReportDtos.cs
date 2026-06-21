using Roofied.Domain.Enums;

namespace Roofied.Application.Reports.Dtos;

/// <summary>
/// A single point for the public map. Contains ONLY fuzzed coordinates and minimal labels.
/// Deliberately has no fields capable of carrying exact location, identity, time, or narrative.
/// </summary>
public sealed record PublicMapPointDto
{
    public required Guid Id { get; init; }
    public required string ReferenceCode { get; init; }
    public required double ApproxLatitude { get; init; }
    public required double ApproxLongitude { get; init; }
    public string? GeneralizedAreaLabel { get; init; }
    public int PrecisionMeters { get; init; }
    public string? GridCellKey { get; init; }
    public string? IncidentTypeName { get; init; }
    public string? VenueCategoryName { get; init; }
    public SuspicionLevel SuspicionLevel { get; init; }
    public DateOnly IncidentDateFrom { get; init; }
    public DateOnly IncidentDateTo { get; init; }
}

/// <summary>Row in the public "latest reports" list.</summary>
public sealed record PublicReportListItemDto
{
    public required Guid Id { get; init; }
    public required string ReferenceCode { get; init; }
    public string? GeneralizedAreaLabel { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? IncidentTypeName { get; init; }
    public string? VenueCategoryName { get; init; }
    public SuspicionLevel SuspicionLevel { get; init; }
    public DateOnly IncidentDateFrom { get; init; }
    public DateOnly IncidentDateTo { get; init; }
    public string? ShortSummary { get; init; }
    public DateTime? PublishedUtc { get; init; }
}

/// <summary>Full public detail page for an approved report. Public-safe fields only.</summary>
public sealed record PublicReportDetailDto
{
    public required Guid Id { get; init; }
    public required string ReferenceCode { get; init; }
    public string? GeneralizedAreaLabel { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? Country { get; init; }
    public string? IncidentTypeName { get; init; }
    public string? VenueCategoryName { get; init; }
    public SuspicionLevel SuspicionLevel { get; init; }
    public DateOnly IncidentDateFrom { get; init; }
    public DateOnly IncidentDateTo { get; init; }
    public bool? MedicalCareSought { get; init; }
    public bool? PoliceReportFiled { get; init; }
    /// <summary>Moderator-authored, privacy-safe summary. Never the raw narrative.</summary>
    public string? PublicSummary { get; init; }
    public IReadOnlyList<string> SafetyTags { get; init; } = Array.Empty<string>();
    public DateTime? PublishedUtc { get; init; }
}
