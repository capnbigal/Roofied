using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Roofied.Application.Abstractions;
using Roofied.Application.Common;
using Roofied.Application.Geo;
using Roofied.Application.Moderation;
using Roofied.Application.Reports;
using Roofied.Domain.Channels;
using Roofied.Domain.Enums;
using Roofied.Domain.Moderation;
using Roofied.Domain.Reports;
using Roofied.Infrastructure.Options;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

public sealed class ModerationService(
    RoofiedDbContext db,
    ILocationPrecisionService precision,
    IHtmlSanitizer sanitizer,
    IClock clock,
    ICurrentUser currentUser,
    IAuditService audit,
    IOptions<LocationPrecisionConfig> locationOptions) : IModerationService
{
    private readonly LocationPrecisionConfig _location = locationOptions.Value;

    // ---------------- Reports ----------------

    public async Task<PagedResult<ModeratorReportListItemDto>> GetReportQueueAsync(
        ReportStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Reports.AsNoTracking().AsQueryable();
        query = status is { } s
            ? query.Where(r => r.Status == s)
            : query.Where(r => r.Status == ReportStatus.PendingReview || r.Status == ReportStatus.NeedsClarification);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CreatedUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ModeratorReportListItemDto
            {
                Id = r.Id,
                ReferenceCode = r.ReferenceCode,
                Status = r.Status,
                Visibility = r.Visibility,
                City = r.City,
                IncidentTypeName = r.ReportCategory!.Name,
                IncidentDateFrom = r.IncidentDateFrom,
                CreatedUtc = r.CreatedUtc,
                AutoFlagCount = r.Restricted != null && r.Restricted.AutoFlagsJson != null ? 1 : 0,
                OpenFlagCount = db.ContentFlags.Count(f =>
                    f.ContentType == ModeratedContentType.Report && f.ContentId == r.Id && f.Status == FlagStatus.Open),
            })
            .ToListAsync(ct);

        return PagedResult<ModeratorReportListItemDto>.Create(items, page, pageSize, total);
    }

    public async Task<ModeratorReportDetailDto?> GetReportForModerationAsync(Guid reportId, CancellationToken ct = default)
    {
        var r = await db.Reports.AsNoTracking()
            .Include(x => x.ReportCategory)
            .Include(x => x.VenueCategory)
            .Include(x => x.Restricted)
            .Include(x => x.PreciseLocation)
            .Include(x => x.PublicLocation)
            .Include(x => x.SafetyTags)
            .Include(x => x.StatusHistory)
            .Include(x => x.ModerationCases).ThenInclude(c => c.Notes)
            .FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (r is null)
            return null;

        var flags = await db.ContentFlags.AsNoTracking()
            .Where(f => f.ContentType == ModeratedContentType.Report && f.ContentId == reportId)
            .OrderByDescending(f => f.CreatedUtc)
            .Select(f => new ContentFlagDto(f.Id, f.ContentType, f.ContentId, f.Reason, f.Details, f.Status, f.CreatedUtc))
            .ToListAsync(ct);

        var autoFlags = r.Restricted?.AutoFlagsJson is { } json
            ? JsonSerializer.Deserialize<List<string>>(json) ?? new()
            : new List<string>();

        var notes = r.ModerationCases.SelectMany(c => c.Notes)
            .OrderByDescending(n => n.CreatedUtc)
            .Select(n => new ModerationNoteDto(n.AuthorUserId, n.Text, n.CreatedUtc))
            .ToList();

        return new ModeratorReportDetailDto
        {
            Id = r.Id,
            ReferenceCode = r.ReferenceCode,
            Status = r.Status,
            Visibility = r.Visibility,
            SuspicionLevel = r.SuspicionLevel,
            IncidentTypeName = r.ReportCategory?.Name,
            VenueCategoryName = r.VenueCategory?.Name,
            ReportCategoryId = r.ReportCategoryId,
            VenueCategoryId = r.VenueCategoryId,
            IncidentDateFrom = r.IncidentDateFrom,
            IncidentDateTo = r.IncidentDateTo,
            City = r.City,
            Region = r.Region,
            Country = r.Country,
            MedicalCareSought = r.MedicalCareSought,
            PoliceReportFiled = r.PoliceReportFiled,
            PublicSummary = r.PublicSummary,
            RawNarrative = r.Restricted?.RawNarrative,
            RedactedNarrative = r.Restricted?.RedactedNarrative,
            SymptomsDescription = r.Restricted?.SymptomsDescription,
            ExactIncidentUtc = r.Restricted?.ExactIncidentUtc,
            PrivateContactMethod = r.Restricted?.PrivateContactMethod,
            PrivateContactValue = r.Restricted?.PrivateContactValue,
            ExactLatitude = r.PreciseLocation?.ExactLatitude,
            ExactLongitude = r.PreciseLocation?.ExactLongitude,
            ExactAddress = r.PreciseLocation?.ExactAddress,
            AutoFlags = autoFlags,
            ApproxLatitude = r.PublicLocation?.ApproxLatitude,
            ApproxLongitude = r.PublicLocation?.ApproxLongitude,
            PrecisionMeters = r.PublicLocation?.PrecisionMeters,
            GeneralizedAreaLabel = r.PublicLocation?.GeneralizedAreaLabel,
            SafetyTags = r.SafetyTags.Select(t => t.Label).ToList(),
            StatusHistory = r.StatusHistory.OrderByDescending(h => h.CreatedUtc)
                .Select(h => new ReportStatusHistoryDto(h.FromStatus, h.ToStatus, h.ChangedByUserId, h.Reason, h.CreatedUtc))
                .ToList(),
            Notes = notes,
            Flags = flags,
        };
    }

    public async Task<OperationResult> ApproveReportAsync(ApproveReportInput input, CancellationToken ct = default)
    {
        var report = await db.Reports
            .Include(r => r.PreciseLocation)
            .Include(r => r.PublicLocation)
            .Include(r => r.SafetyTags)
            .Include(r => r.ModerationCases)
            .FirstOrDefaultAsync(r => r.Id == input.ReportId, ct);
        if (report is null)
            return OperationResult.Fail("Report not found.");
        if (!ReportWorkflow.CanTransition(report.Status, ReportStatus.Approved))
            return OperationResult.Fail($"Cannot approve a report in status {report.Status}.");

        report.PublicSummary = sanitizer.SanitizePlainText(input.PublicSummary);

        // Compute / refresh the public fuzzed location from the precise coordinates.
        var label = string.IsNullOrWhiteSpace(input.GeneralizedAreaLabel)
            ? BuildAreaLabel(report)
            : sanitizer.SanitizePlainText(input.GeneralizedAreaLabel);
        ApplyPublicLocation(report, input.PrecisionMetersOverride, label);

        // Replace safety tags with the moderator-approved set.
        if (report.SafetyTags.Count > 0)
            db.ReportSafetyTags.RemoveRange(report.SafetyTags);
        foreach (var tag in input.SafetyTags.Select(t => sanitizer.SanitizePlainText(t)).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct())
            db.ReportSafetyTags.Add(new ReportSafetyTag { ReportId = report.Id, Label = tag });

        TransitionTo(report, ReportStatus.Approved, "Approved and published.");
        report.PublishedUtc = clock.UtcNow;
        ResolveCases(report);

        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Report.Approved", currentUser.UserId, currentUser.DisplayName, nameof(Report),
            report.Id.ToString(), $"Report {report.ReferenceCode} approved.", ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    public Task<OperationResult> RejectReportAsync(Guid reportId, string reason, CancellationToken ct = default) =>
        SimpleTransitionAsync(reportId, ReportStatus.Rejected, reason, "Report.Rejected", ct);

    public Task<OperationResult> RequestClarificationAsync(Guid reportId, string note, CancellationToken ct = default) =>
        SimpleTransitionAsync(reportId, ReportStatus.NeedsClarification, note, "Report.ClarificationRequested", ct, alsoNote: true);

    public Task<OperationResult> ArchiveReportAsync(Guid reportId, string reason, CancellationToken ct = default) =>
        SimpleTransitionAsync(reportId, ReportStatus.Archived, reason, "Report.Archived", ct);

    public async Task<OperationResult> RedactNarrativeAsync(Guid reportId, string redactedNarrative, CancellationToken ct = default)
    {
        var restricted = await db.ReportRestricted.FirstOrDefaultAsync(r => r.ReportId == reportId, ct);
        if (restricted is null)
            return OperationResult.Fail("Report content not found.");
        restricted.RedactedNarrative = sanitizer.SanitizePlainText(redactedNarrative);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Report.NarrativeRedacted", currentUser.UserId, currentUser.DisplayName, nameof(Report),
            reportId.ToString(), "Narrative redacted.", ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult> AdjustPrecisionAsync(Guid reportId, int gridSizeMeters, CancellationToken ct = default)
    {
        var report = await db.Reports
            .Include(r => r.PreciseLocation)
            .Include(r => r.PublicLocation)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
            return OperationResult.Fail("Report not found.");
        if (report.PreciseLocation?.ExactLatitude is null)
            return OperationResult.Fail("This report has no precise location to generalize.");

        ApplyPublicLocation(report, gridSizeMeters, report.PublicLocation?.GeneralizedAreaLabel ?? BuildAreaLabel(report));
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Report.PrecisionAdjusted", currentUser.UserId, currentUser.DisplayName, nameof(Report),
            reportId.ToString(), $"Precision set to {gridSizeMeters}m.", ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult> AddNoteAsync(Guid reportId, string text, CancellationToken ct = default)
    {
        var moderationCase = await db.ModerationCases.FirstOrDefaultAsync(c => c.ReportId == reportId, ct);
        if (moderationCase is null)
        {
            moderationCase = new ModerationCase { ReportId = reportId, State = ModerationCaseState.InProgress };
            db.ModerationCases.Add(moderationCase);
        }
        db.ModerationNotes.Add(new ModerationNote
        {
            ModerationCaseId = moderationCase.Id,
            AuthorUserId = currentUser.UserId ?? "system",
            Text = sanitizer.SanitizePlainText(text),
        });
        await db.SaveChangesAsync(ct);
        return OperationResult.Success();
    }

    // ---------------- Channel posts ----------------

    public async Task<PagedResult<ChannelPostModerationDto>> GetPostQueueAsync(
        ChannelPostStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ChannelPosts.AsNoTracking().AsQueryable();
        query = status is { } s ? query.Where(p => p.Status == s) : query.Where(p => p.Status == ChannelPostStatus.PendingReview);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.CreatedUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ChannelPostModerationDto
            {
                Id = p.Id,
                ChannelName = p.Channel!.Name,
                Title = p.Title,
                Body = p.RedactedBody ?? p.Body,
                AuthorDisplayName = p.AuthorDisplayName,
                Status = p.Status,
                IsPinned = p.IsPinned,
                IsLocked = p.IsLocked,
                IsHidden = p.IsHidden,
                OpenFlagCount = db.ContentFlags.Count(f =>
                    f.ContentType == ModeratedContentType.ChannelPost && f.ContentId == p.Id && f.Status == FlagStatus.Open),
                CreatedUtc = p.CreatedUtc,
            })
            .ToListAsync(ct);
        return PagedResult<ChannelPostModerationDto>.Create(items, page, pageSize, total);
    }

    public Task<OperationResult> ApprovePostAsync(Guid postId, CancellationToken ct = default) =>
        UpdatePostAsync(postId, p =>
        {
            p.Status = ChannelPostStatus.Approved;
            p.IsHidden = false;
            p.ModeratedByUserId = currentUser.UserId;
            p.PublishedUtc ??= clock.UtcNow;
        }, "ChannelPost.Approved", ct);

    public Task<OperationResult> RejectPostAsync(Guid postId, string reason, CancellationToken ct = default) =>
        UpdatePostAsync(postId, p =>
        {
            p.Status = ChannelPostStatus.Rejected;
            p.ModeratedByUserId = currentUser.UserId;
        }, "ChannelPost.Rejected", ct);

    public Task<OperationResult> HidePostAsync(Guid postId, CancellationToken ct = default) =>
        UpdatePostAsync(postId, p =>
        {
            p.IsHidden = true;
            p.Status = ChannelPostStatus.Hidden;
            p.ModeratedByUserId = currentUser.UserId;
        }, "ChannelPost.Hidden", ct);

    public Task<OperationResult> SetPostPinnedAsync(Guid postId, bool pinned, CancellationToken ct = default) =>
        UpdatePostAsync(postId, p => p.IsPinned = pinned, "ChannelPost.Pinned", ct);

    public Task<OperationResult> SetPostLockedAsync(Guid postId, bool locked, CancellationToken ct = default) =>
        UpdatePostAsync(postId, p => p.IsLocked = locked, "ChannelPost.Locked", ct);

    public Task<OperationResult> RedactPostAsync(Guid postId, string redactedBody, CancellationToken ct = default) =>
        UpdatePostAsync(postId, p => p.RedactedBody = sanitizer.SanitizePlainText(redactedBody), "ChannelPost.Redacted", ct);

    // ---------------- Flags ----------------

    public async Task<PagedResult<ContentFlagDto>> GetFlagsAsync(FlagStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ContentFlags.AsNoTracking().AsQueryable();
        query = status is { } s ? query.Where(f => f.Status == s) : query.Where(f => f.Status == FlagStatus.Open);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.CreatedUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new ContentFlagDto(f.Id, f.ContentType, f.ContentId, f.Reason, f.Details, f.Status, f.CreatedUtc))
            .ToListAsync(ct);
        return PagedResult<ContentFlagDto>.Create(items, page, pageSize, total);
    }

    public async Task<OperationResult> ResolveFlagAsync(Guid flagId, string resolutionNote, CancellationToken ct = default)
    {
        var flag = await db.ContentFlags.FirstOrDefaultAsync(f => f.Id == flagId, ct);
        if (flag is null) return OperationResult.Fail("Flag not found.");
        flag.Status = FlagStatus.Resolved;
        flag.ResolvedByUserId = currentUser.UserId;
        flag.ResolutionNote = sanitizer.SanitizePlainText(resolutionNote);
        flag.ResolvedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Flag.Resolved", currentUser.UserId, currentUser.DisplayName, nameof(Domain.Moderation.ContentFlag),
            flagId.ToString(), ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult> DismissFlagAsync(Guid flagId, string reason, CancellationToken ct = default)
    {
        var flag = await db.ContentFlags.FirstOrDefaultAsync(f => f.Id == flagId, ct);
        if (flag is null) return OperationResult.Fail("Flag not found.");
        flag.Status = FlagStatus.Dismissed;
        flag.ResolvedByUserId = currentUser.UserId;
        flag.ResolutionNote = sanitizer.SanitizePlainText(reason);
        flag.ResolvedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Flag.Dismissed", currentUser.UserId, currentUser.DisplayName, nameof(Domain.Moderation.ContentFlag),
            flagId.ToString(), ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    // ---------------- Helpers ----------------

    private async Task<OperationResult> SimpleTransitionAsync(
        Guid reportId, ReportStatus to, string reason, string auditAction, CancellationToken ct, bool alsoNote = false)
    {
        var report = await db.Reports.Include(r => r.ModerationCases).FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (report is null)
            return OperationResult.Fail("Report not found.");
        if (!ReportWorkflow.CanTransition(report.Status, to))
            return OperationResult.Fail($"Cannot move report from {report.Status} to {to}.");

        var cleanReason = sanitizer.SanitizePlainText(reason);
        TransitionTo(report, to, cleanReason);

        if (to is ReportStatus.Rejected or ReportStatus.Archived)
        {
            report.PublishedUtc = null;
            ResolveCases(report);
        }
        if (alsoNote)
        {
            var moderationCase = report.ModerationCases.FirstOrDefault() ?? AddCase(report);
            db.ModerationNotes.Add(new ModerationNote
            {
                ModerationCaseId = moderationCase.Id,
                AuthorUserId = currentUser.UserId ?? "system",
                Text = cleanReason,
            });
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(auditAction, currentUser.UserId, currentUser.DisplayName, nameof(Report),
            report.Id.ToString(), $"Report {report.ReferenceCode} -> {to}.", ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    private async Task<OperationResult> UpdatePostAsync(Guid postId, Action<ChannelPost> mutate, string auditAction, CancellationToken ct)
    {
        var post = await db.ChannelPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return OperationResult.Fail("Post not found.");
        mutate(post);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(auditAction, currentUser.UserId, currentUser.DisplayName, nameof(ChannelPost),
            postId.ToString(), ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    private void TransitionTo(Report report, ReportStatus to, string? reason)
    {
        var from = report.Status;
        report.Status = to;
        // Add through the DbSet so EF marks it Added (child of an already-tracked parent).
        db.ReportStatusHistory.Add(new ReportStatusHistory
        {
            ReportId = report.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = currentUser.UserId,
            Reason = reason,
        });
    }

    private void ApplyPublicLocation(Report report, int? gridOverride, string? label)
    {
        if (report.PreciseLocation?.ExactLatitude is not { } lat || report.PreciseLocation.ExactLongitude is not { } lon)
            return;

        var opts = new LocationPrecisionOptions
        {
            GridSizeMeters = gridOverride ?? _location.DefaultGridSizeMeters,
            MinGridSizeMeters = _location.MinGridSizeMeters,
        };
        var fuzzed = precision.Fuzz(lat, lon, opts);

        var isNew = report.PublicLocation is null;
        var loc = report.PublicLocation ?? new ReportPublicLocation { ReportId = report.Id };
        loc.ApproxLatitude = fuzzed.ApproxLatitude;
        loc.ApproxLongitude = fuzzed.ApproxLongitude;
        loc.PrecisionMeters = fuzzed.PrecisionMeters;
        loc.GridCellKey = fuzzed.GridCellKey;
        loc.GeneralizedAreaLabel = label;
        if (isNew)
        {
            report.PublicLocation = loc;
            db.ReportPublicLocations.Add(loc); // force Added for a new 1:1 dependent
        }
    }

    private static string BuildAreaLabel(Report report) =>
        string.IsNullOrWhiteSpace(report.Region) ? report.City : $"{report.City}, {report.Region}";

    private void ResolveCases(Report report)
    {
        foreach (var c in report.ModerationCases.Where(c => c.State != ModerationCaseState.Resolved))
        {
            c.State = ModerationCaseState.Resolved;
            c.ResolvedUtc = clock.UtcNow;
            c.ResolvedByUserId = currentUser.UserId;
        }
    }

    private ModerationCase AddCase(Report report)
    {
        var c = new ModerationCase { ReportId = report.Id, State = ModerationCaseState.InProgress };
        db.ModerationCases.Add(c);
        return c;
    }
}
