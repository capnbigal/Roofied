using Roofied.Domain.Enums;

namespace Roofied.Application.Reports;

/// <summary>
/// Captures everything a user submits on the report form. Sensitive fields here are routed to the
/// restricted tables by the service; only moderated, public-safe fields ever reach public surfaces.
/// </summary>
public sealed class ReportSubmissionInput
{
    // Incident timing
    public DateOnly? IncidentDate { get; set; }
    public TimeOnly? ApproximateTime { get; set; }

    // Approximate location chosen by the user (treated as precise input and fuzzed before publishing).
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string City { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? Country { get; set; }

    // Categorization
    public Guid? ReportCategoryId { get; set; }
    public Guid? VenueCategoryId { get; set; }
    public SuspicionLevel SuspicionLevel { get; set; } = SuspicionLevel.Unknown;

    // Narrative & concerns (restricted; never published verbatim)
    public string? Narrative { get; set; }
    public string? Symptoms { get; set; }

    public bool? MedicalCareSought { get; set; }
    public bool? PoliceReportFiled { get; set; }

    // Optional private contact for moderator follow-up (stored separately; never public)
    public string? PrivateContactMethod { get; set; }
    public string? PrivateContactValue { get; set; }

    // Identity / visibility choices
    public bool PostAnonymously { get; set; } = true;
    public ReportVisibility Visibility { get; set; } = ReportVisibility.Public;

    // Consent
    public bool ConsentAcknowledged { get; set; }
    public bool SafetyNoticeAcknowledged { get; set; }

    // Bot protection
    public string? CaptchaToken { get; set; }
}

/// <summary>Filter criteria for public map/list queries.</summary>
public sealed record PublicReportFilter
{
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? City { get; init; }
    public Guid? ReportCategoryId { get; init; }
    public Guid? VenueCategoryId { get; init; }
    public SuspicionLevel? SuspicionLevel { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
