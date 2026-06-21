using Roofied.Application.Common;
using Roofied.Application.Reports.Dtos;
using Roofied.Domain.Enums;

namespace Roofied.Application.Reports;

/// <summary>A report as seen by its owner (registered user managing their own submissions).</summary>
public sealed record MyReportDto
{
    public required Guid Id { get; init; }
    public required string ReferenceCode { get; init; }
    public ReportStatus Status { get; init; }
    public ReportVisibility Visibility { get; init; }
    public string? City { get; init; }
    public string? IncidentTypeName { get; init; }
    public DateOnly IncidentDateFrom { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? PublishedUtc { get; init; }
}

public interface IReportService
{
    /// <summary>
    /// Submits a new report. Applies validation, sanitization, PII flagging, fuzzing and rate limiting.
    /// On success, returns the friendly report reference code (e.g. "RPT-7F3K9Q").
    /// </summary>
    Task<OperationResult<string>> SubmitAsync(ReportSubmissionInput input, CancellationToken ct = default);

    /// <summary>Public, fuzzed map points matching the filter. Approved + public only.</summary>
    Task<IReadOnlyList<PublicMapPointDto>> GetMapPointsAsync(PublicReportFilter filter, CancellationToken ct = default);

    /// <summary>Public, paginated latest-reports list. Approved + public only.</summary>
    Task<PagedResult<PublicReportListItemDto>> GetPublicListAsync(PublicReportFilter filter, CancellationToken ct = default);

    /// <summary>Public report detail by id. Returns null if not publicly visible.</summary>
    Task<PublicReportDetailDto?> GetPublicDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>Reports owned by the current user.</summary>
    Task<IReadOnlyList<MyReportDto>> GetMyReportsAsync(string userId, CancellationToken ct = default);

    /// <summary>Owner withdraws a pending report (soft-deletes / archives it).</summary>
    Task<OperationResult> WithdrawAsync(Guid reportId, string userId, CancellationToken ct = default);
}
