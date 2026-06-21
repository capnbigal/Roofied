using Roofied.Domain.Common;
using Roofied.Domain.Enums;
using Roofied.Domain.Moderation;

namespace Roofied.Domain.Reports;

/// <summary>
/// The report aggregate root. This table holds ONLY public-safe, moderated fields plus
/// workflow/ownership metadata. Sensitive content lives in separate, access-restricted
/// satellite tables:
/// <list type="bullet">
///   <item><see cref="Restricted"/> — raw narrative, symptoms, exact time, private contact.</item>
///   <item><see cref="PreciseLocation"/> — exact coordinates / address.</item>
///   <item><see cref="PublicLocation"/> — intentionally fuzzed coordinates for the public map.</item>
/// </list>
/// The public projection (see Application layer) selects from this entity only and never
/// references the restricted satellites, so restricted data cannot leak by construction.
/// </summary>
public class Report : FullAuditableEntity
{
    /// <summary>Short human-friendly reference code (e.g. "RPT-7F3K9Q"). Unique.</summary>
    public required string ReferenceCode { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.PendingReview;
    public ReportVisibility Visibility { get; set; } = ReportVisibility.Public;
    public SuspicionLevel SuspicionLevel { get; set; } = SuspicionLevel.Unknown;

    // --- Categorization (public-safe) ---
    public Guid ReportCategoryId { get; set; }
    public ReportCategory? ReportCategory { get; set; }

    public Guid? VenueCategoryId { get; set; }
    public VenueCategory? VenueCategory { get; set; }

    // --- Coarse date range (public-safe). Exact time is restricted. ---
    public DateOnly IncidentDateFrom { get; set; }
    public DateOnly IncidentDateTo { get; set; }

    // --- Generalized location (public-safe). No street address here. ---
    public required string City { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }

    // --- Public-safe aggregate facts (yes/no only, never free text) ---
    public bool? MedicalCareSought { get; set; }
    public bool? PoliceReportFiled { get; set; }

    /// <summary>
    /// Moderator-authored, privacy-safe summary shown publicly. Null until a moderator approves
    /// the report and writes/redacts a summary. The raw user narrative is never published directly.
    /// </summary>
    public string? PublicSummary { get; set; }

    /// <summary>Ownership for account holders. Null for anonymous reports. NEVER exposed publicly.</summary>
    public string? CreatedByUserId { get; set; }

    public DateTime? PublishedUtc { get; set; }

    // --- Navigation ---
    public ReportRestricted? Restricted { get; set; }
    public ReportLocation? PreciseLocation { get; set; }
    public ReportPublicLocation? PublicLocation { get; set; }
    public ICollection<ReportSafetyTag> SafetyTags { get; set; } = new List<ReportSafetyTag>();
    public ICollection<ReportStatusHistory> StatusHistory { get; set; } = new List<ReportStatusHistory>();
    public ICollection<ModerationCase> ModerationCases { get; set; } = new List<ModerationCase>();
}

/// <summary>
/// Restricted satellite holding raw, sensitive report content. Access is limited to authorized
/// moderators via moderator-scoped services. Never include this entity in a public query/DTO.
/// </summary>
public class ReportRestricted : IAuditableEntity
{
    public Guid ReportId { get; set; }
    public Report? Report { get; set; }

    /// <summary>The reporter's original narrative ("what happened"). Restricted.</summary>
    public string? RawNarrative { get; set; }

    /// <summary>Moderator-redacted version of the narrative (working copy before publishing).</summary>
    public string? RedactedNarrative { get; set; }

    /// <summary>Symptoms / concerns free text. Restricted.</summary>
    public string? SymptomsDescription { get; set; }

    /// <summary>Exact incident timestamp. Restricted (only a coarse range is public).</summary>
    public DateTime? ExactIncidentUtc { get; set; }

    /// <summary>Optional private contact channel for moderator follow-up. Never exposed publicly.</summary>
    public string? PrivateContactMethod { get; set; }
    public string? PrivateContactValue { get; set; }

    /// <summary>Salted hash of submitter IP, for abuse correlation only (never the raw IP).</summary>
    public string? SubmitterIpHash { get; set; }

    /// <summary>Auto-detected potential PII / accusation markers as a JSON array, for moderator attention.</summary>
    public string? AutoFlagsJson { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

/// <summary>
/// Restricted precise location. Stored only when provided and protected separately. Moderator-only.
/// </summary>
public class ReportLocation : IAuditableEntity
{
    public Guid ReportId { get; set; }
    public Report? Report { get; set; }

    public double? ExactLatitude { get; set; }
    public double? ExactLongitude { get; set; }
    public string? ExactAddress { get; set; }
    public string? LocationNotes { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

/// <summary>
/// Public-safe, intentionally imprecise location used for the public map. Computed by the
/// location-precision service at approval time. Contains no exact coordinates or address.
/// </summary>
public class ReportPublicLocation : IAuditableEntity
{
    public Guid ReportId { get; set; }
    public Report? Report { get; set; }

    /// <summary>Fuzzed latitude (snapped to a grid + deterministic jitter).</summary>
    public double ApproxLatitude { get; set; }

    /// <summary>Fuzzed longitude (snapped to a grid + deterministic jitter).</summary>
    public double ApproxLongitude { get; set; }

    /// <summary>Human-readable generalized area label (e.g. "Central district, Springfield").</summary>
    public string? GeneralizedAreaLabel { get; set; }

    /// <summary>Approximate precision radius in meters that the fuzzing guarantees.</summary>
    public int PrecisionMeters { get; set; }

    /// <summary>Stable grid-cell key used for clustering without revealing exact points.</summary>
    public string? GridCellKey { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

/// <summary>Public-safe safety tag attached to an approved report (e.g. "open container", "lost time").</summary>
public class ReportSafetyTag : AuditableEntity
{
    public Guid ReportId { get; set; }
    public Report? Report { get; set; }
    public required string Label { get; set; }
}

/// <summary>Immutable audit row recording each report status transition.</summary>
public class ReportStatusHistory : AuditableEntity
{
    public Guid ReportId { get; set; }
    public Report? Report { get; set; }

    public ReportStatus? FromStatus { get; set; }
    public ReportStatus ToStatus { get; set; }

    public string? ChangedByUserId { get; set; }

    /// <summary>Moderator-only reason/notes for the transition.</summary>
    public string? Reason { get; set; }
}
