using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Roofied.Application.Abstractions;
using Roofied.Application.Common;
using Roofied.Application.Reports;
using Roofied.Application.Reports.Dtos;
using Roofied.Application.Safety;
using Roofied.Domain.Consent;
using Roofied.Domain.Enums;
using Roofied.Domain.Moderation;
using Roofied.Domain.Reports;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

public sealed class ReportService(
    IDbContextFactory<RoofiedDbContext> dbFactory,
    IValidator<ReportSubmissionInput> validator,
    IHtmlSanitizer sanitizer,
    IPiiDetectionService pii,
    IReferenceCodeGenerator referenceCodes,
    IRateLimitService rateLimiter,
    ICaptchaVerifier captcha,
    IClock clock,
    ICurrentUser currentUser,
    IAuditService audit) : IReportService
{
    private const string ConsentVersion = "report-consent-v1";

    public async Task<OperationResult<string>> SubmitAsync(ReportSubmissionInput input, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return OperationResult<string>.Fail(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

        // Abuse controls: captcha + durable rate limit.
        if (!await captcha.VerifyAsync(input.CaptchaToken, null, ct))
            return OperationResult<string>.Fail("Bot verification failed. Please try again.");

        var clientKey = ResolveClientKey();
        var rate = await rateLimiter.CheckAndRecordAsync(RateLimitAction.ReportSubmit, clientKey, ct);
        if (!rate.Allowed)
            return OperationResult<string>.Fail("You have submitted several reports recently. Please try again later.");

        // Sanitize all free text. Narrative/symptoms are restricted (never published verbatim).
        var narrative = sanitizer.SanitizePlainText(input.Narrative);
        var symptoms = sanitizer.SanitizePlainText(input.Symptoms);
        var city = sanitizer.SanitizePlainText(input.City);
        var region = sanitizer.SanitizePlainText(input.Region);
        var country = sanitizer.SanitizePlainText(input.Country);

        // Heuristic PII / accusation detection across narrative + symptoms, for moderator attention.
        var findings = pii.Detect(narrative).Findings
            .Concat(pii.Detect(symptoms).Findings)
            .Select(f => $"{f.Kind}: {f.Sample}")
            .ToList();

        var isDraft = input.Visibility == ReportVisibility.PersonalDraft && currentUser.IsAuthenticated;
        var status = isDraft ? ReportStatus.Draft : ReportStatus.PendingReview;

        var incidentDate = input.IncidentDate ?? DateOnly.FromDateTime(clock.UtcNow);

        var report = new Report
        {
            ReferenceCode = referenceCodes.Generate(),
            Status = status,
            Visibility = input.Visibility,
            SuspicionLevel = input.SuspicionLevel,
            ReportCategoryId = input.ReportCategoryId!.Value,
            VenueCategoryId = input.VenueCategoryId,
            IncidentDateFrom = incidentDate,
            IncidentDateTo = incidentDate,
            City = string.IsNullOrWhiteSpace(city) ? "Unspecified" : city,
            Region = NullIfEmpty(region),
            Country = NullIfEmpty(country),
            MedicalCareSought = input.MedicalCareSought,
            PoliceReportFiled = input.PoliceReportFiled,
            CreatedByUserId = currentUser.IsAuthenticated && !input.PostAnonymously ? currentUser.UserId : null,
            Restricted = new ReportRestricted
            {
                RawNarrative = NullIfEmpty(narrative),
                SymptomsDescription = NullIfEmpty(symptoms),
                ExactIncidentUtc = ComposeIncidentTimestamp(input),
                PrivateContactMethod = NullIfEmpty(sanitizer.SanitizePlainText(input.PrivateContactMethod)),
                PrivateContactValue = NullIfEmpty(sanitizer.SanitizePlainText(input.PrivateContactValue)),
                SubmitterIpHash = currentUser.IpHash,
                AutoFlagsJson = findings.Count > 0 ? JsonSerializer.Serialize(findings) : null,
            },
        };

        // Store precise coordinates only when supplied; fuzzing happens at approval time.
        if (input.Latitude.HasValue && input.Longitude.HasValue)
        {
            report.PreciseLocation = new ReportLocation
            {
                ExactLatitude = input.Latitude,
                ExactLongitude = input.Longitude,
            };
        }

        report.StatusHistory.Add(new ReportStatusHistory
        {
            FromStatus = null,
            ToStatus = status,
            ChangedByUserId = report.CreatedByUserId,
            Reason = "Report submitted.",
        });

        if (!isDraft)
        {
            report.ModerationCases.Add(new ModerationCase
            {
                State = ModerationCaseState.Open,
                Priority = findings.Count > 0 ? ModerationPriority.High : ModerationPriority.Normal,
            });
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Reports.Add(report);

        db.ConsentRecords.Add(new ConsentRecord
        {
            ConsentType = "ReportSubmission",
            ConsentTextVersion = ConsentVersion,
            UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
            ReportId = report.Id,
            IpHash = currentUser.IpHash,
        });

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(
            "Report.Submitted",
            actorUserId: report.CreatedByUserId,
            entityType: nameof(Report),
            entityId: report.Id.ToString(),
            summary: $"Report {report.ReferenceCode} submitted ({status}).",
            metadata: new { report.ReferenceCode, AutoFlags = findings.Count },
            ipHash: currentUser.IpHash,
            ct: ct);

        return OperationResult<string>.Success(report.ReferenceCode);
    }

    public async Task<IReadOnlyList<PublicMapPointDto>> GetMapPointsAsync(PublicReportFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Reports.AsNoTracking()
            .Where(PublicReportProjections.IsPubliclyVisible)
            .Where(r => r.PublicLocation != null);
        query = ApplyFilter(query, filter);
        return await query.Select(PublicReportProjections.ToMapPoint).Take(2000).ToListAsync(ct);
    }

    public async Task<PagedResult<PublicReportListItemDto>> GetPublicListAsync(PublicReportFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Reports.AsNoTracking().Where(PublicReportProjections.IsPubliclyVisible);
        query = ApplyFilter(query, filter);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(r => r.PublishedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(PublicReportProjections.ToListItem)
            .ToListAsync(ct);

        return PagedResult<PublicReportListItemDto>.Create(items, page, pageSize, total);
    }

    public async Task<PublicReportDetailDto?> GetPublicDetailAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Reports.AsNoTracking()
            .Where(PublicReportProjections.IsPubliclyVisible)
            .Where(r => r.Id == id)
            .Select(PublicReportProjections.ToDetail)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MyReportDto>> GetMyReportsAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Reports.AsNoTracking()
            .Where(r => r.CreatedByUserId == userId)
            .OrderByDescending(r => r.CreatedUtc)
            .Select(r => new MyReportDto
            {
                Id = r.Id,
                ReferenceCode = r.ReferenceCode,
                Status = r.Status,
                Visibility = r.Visibility,
                City = r.City,
                IncidentTypeName = r.ReportCategory!.Name,
                IncidentDateFrom = r.IncidentDateFrom,
                CreatedUtc = r.CreatedUtc,
                PublishedUtc = r.PublishedUtc,
            })
            .ToListAsync(ct);
    }

    public async Task<OperationResult> WithdrawAsync(Guid reportId, string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == reportId && r.CreatedByUserId == userId, ct);
        if (report is null)
            return OperationResult.Fail("Report not found.");

        if (report.Status is ReportStatus.Archived)
            return OperationResult.Fail("This report can no longer be withdrawn.");

        var from = report.Status;
        report.Status = ReportStatus.Archived;
        report.IsDeleted = true;
        report.DeletedUtc = clock.UtcNow;
        db.ReportStatusHistory.Add(new ReportStatusHistory
        {
            ReportId = report.Id,
            FromStatus = from,
            ToStatus = ReportStatus.Archived,
            ChangedByUserId = userId,
            Reason = "Withdrawn by reporter.",
        });
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("Report.Withdrawn", actorUserId: userId, entityType: nameof(Report),
            entityId: report.Id.ToString(), summary: $"Report {report.ReferenceCode} withdrawn.", ct: ct);

        return OperationResult.Success();
    }

    private static IQueryable<Report> ApplyFilter(IQueryable<Report> query, PublicReportFilter filter)
    {
        if (filter.FromDate is { } from)
            query = query.Where(r => r.IncidentDateTo >= from);
        if (filter.ToDate is { } to)
            query = query.Where(r => r.IncidentDateFrom <= to);
        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(r => r.City == filter.City);
        if (filter.ReportCategoryId is { } cat)
            query = query.Where(r => r.ReportCategoryId == cat);
        if (filter.VenueCategoryId is { } venue)
            query = query.Where(r => r.VenueCategoryId == venue);
        if (filter.SuspicionLevel is { } level)
            query = query.Where(r => r.SuspicionLevel == level);
        return query;
    }

    private DateTime? ComposeIncidentTimestamp(ReportSubmissionInput input)
    {
        if (input.IncidentDate is not { } date)
            return null;
        var time = input.ApproximateTime ?? new TimeOnly(0, 0);
        return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, DateTimeKind.Utc);
    }

    private string ResolveClientKey() =>
        currentUser.UserId ?? currentUser.IpHash ?? "anonymous";

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
