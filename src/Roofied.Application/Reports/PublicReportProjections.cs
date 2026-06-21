using System.Linq.Expressions;
using Roofied.Application.Reports.Dtos;
using Roofied.Domain.Enums;
using Roofied.Domain.Reports;

namespace Roofied.Application.Reports;

/// <summary>
/// THE public safety boundary for reports.
///
/// Every public-facing read of report data MUST go through one of these projections. They are
/// <see cref="Expression{TDelegate}"/> projections that reference only public-safe columns on
/// <see cref="Report"/> and its <see cref="Report.PublicLocation"/> / category navigations.
///
/// By construction they never touch <see cref="Report.Restricted"/>, <see cref="Report.PreciseLocation"/>,
/// <see cref="Report.CreatedByUserId"/>, or any other restricted field — so exact coordinates, private
/// contact info, raw narrative, reporter identity, and internal notes cannot leak through a public query.
///
/// Tests assert this property structurally; do not add restricted fields to these expressions.
/// </summary>
public static class PublicReportProjections
{
    /// <summary>Predicate for content eligible to appear on public surfaces.</summary>
    public static readonly Expression<Func<Report, bool>> IsPubliclyVisible = r =>
        !r.IsDeleted
        && r.Status == ReportStatus.Approved
        && r.Visibility == ReportVisibility.Public;

    public static readonly Expression<Func<Report, PublicMapPointDto>> ToMapPoint = r => new PublicMapPointDto
    {
        Id = r.Id,
        ReferenceCode = r.ReferenceCode,
        ApproxLatitude = r.PublicLocation!.ApproxLatitude,
        ApproxLongitude = r.PublicLocation!.ApproxLongitude,
        GeneralizedAreaLabel = r.PublicLocation!.GeneralizedAreaLabel,
        PrecisionMeters = r.PublicLocation!.PrecisionMeters,
        GridCellKey = r.PublicLocation!.GridCellKey,
        IncidentTypeName = r.ReportCategory!.Name,
        VenueCategoryName = r.VenueCategory != null ? r.VenueCategory.Name : null,
        SuspicionLevel = r.SuspicionLevel,
        IncidentDateFrom = r.IncidentDateFrom,
        IncidentDateTo = r.IncidentDateTo,
    };

    public static readonly Expression<Func<Report, PublicReportListItemDto>> ToListItem = r => new PublicReportListItemDto
    {
        Id = r.Id,
        ReferenceCode = r.ReferenceCode,
        GeneralizedAreaLabel = r.PublicLocation != null ? r.PublicLocation.GeneralizedAreaLabel : null,
        City = r.City,
        Region = r.Region,
        IncidentTypeName = r.ReportCategory!.Name,
        VenueCategoryName = r.VenueCategory != null ? r.VenueCategory.Name : null,
        SuspicionLevel = r.SuspicionLevel,
        IncidentDateFrom = r.IncidentDateFrom,
        IncidentDateTo = r.IncidentDateTo,
        ShortSummary = r.PublicSummary,
        PublishedUtc = r.PublishedUtc,
    };

    public static readonly Expression<Func<Report, PublicReportDetailDto>> ToDetail = r => new PublicReportDetailDto
    {
        Id = r.Id,
        ReferenceCode = r.ReferenceCode,
        GeneralizedAreaLabel = r.PublicLocation != null ? r.PublicLocation.GeneralizedAreaLabel : null,
        City = r.City,
        Region = r.Region,
        Country = r.Country,
        IncidentTypeName = r.ReportCategory!.Name,
        VenueCategoryName = r.VenueCategory != null ? r.VenueCategory.Name : null,
        SuspicionLevel = r.SuspicionLevel,
        IncidentDateFrom = r.IncidentDateFrom,
        IncidentDateTo = r.IncidentDateTo,
        MedicalCareSought = r.MedicalCareSought,
        PoliceReportFiled = r.PoliceReportFiled,
        PublicSummary = r.PublicSummary,
        SafetyTags = r.SafetyTags.Select(t => t.Label).ToList(),
        PublishedUtc = r.PublishedUtc,
    };
}
