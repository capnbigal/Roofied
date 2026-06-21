using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Roofied.Application.Geo;
using Roofied.Application.Moderation;
using Roofied.Application.Reports;
using Roofied.Application.Reports.Validation;
using Roofied.Application.Safety;
using Roofied.Domain.Enums;
using Roofied.Domain.Reports;
using Roofied.Infrastructure.Options;
using Roofied.Infrastructure.Persistence;
using Roofied.Infrastructure.Services;

namespace Roofied.Tests;

public class ReportRedactionIntegrationTests
{
    private const string SecretNarrative = "It happened at 123 Main Street. His name is John Smith, call 555-987-6543.";
    private const string SecretContact = "victim-private@example.com";
    private const double ExactLat = 40.712830;
    private const double ExactLng = -74.006015;

    private sealed class Harness : IDisposable
    {
        public RoofiedDbContext Db { get; }
        public FakeClock Clock { get; } = new();
        public FakeCurrentUser User { get; } = new();
        public ReportService Reports { get; }
        public ModerationService Moderation { get; }
        public Guid CategoryId { get; }

        public Harness()
        {
            Db = TestDb.Create(Clock);
            var sanitizer = TestDb.Sanitizer();
            var audit = new AuditService(Db);
            var rate = new RateLimitService(Db, Clock, Options.Create(new RateLimitOptions()));

            Reports = new ReportService(Db, new ReportSubmissionInputValidator(), sanitizer,
                new PiiDetectionService(), new Roofied.Application.Common.ReferenceCodeGenerator(),
                rate, new AlwaysPassCaptcha(), Clock, User, audit);

            Moderation = new ModerationService(Db, new LocationPrecisionService(), sanitizer,
                Clock, User, audit, Options.Create(new LocationPrecisionConfig()));

            var category = new ReportCategory { Name = "Suspected", Slug = "suspected" };
            Db.ReportCategories.Add(category);
            Db.SaveChanges();
            CategoryId = category.Id;
        }

        public ReportSubmissionInput SampleInput() => new()
        {
            City = "Springfield",
            Region = "IL",
            ReportCategoryId = CategoryId,
            IncidentDate = new DateOnly(2026, 1, 1),
            ApproximateTime = new TimeOnly(23, 15),
            SuspicionLevel = SuspicionLevel.Suspected,
            Narrative = SecretNarrative,
            Symptoms = "Dizzy and disoriented.",
            PrivateContactMethod = "email",
            PrivateContactValue = SecretContact,
            Latitude = ExactLat,
            Longitude = ExactLng,
            Visibility = ReportVisibility.Public,
            SafetyNoticeAcknowledged = true,
            ConsentAcknowledged = true,
        };

        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task Submitted_report_is_pending_and_not_public()
    {
        using var h = new Harness();
        var result = await h.Reports.SubmitAsync(h.SampleInput());
        Assert.True(result.Succeeded);

        var report = await h.Db.Reports.Include(r => r.Restricted).Include(r => r.PreciseLocation).SingleAsync();
        Assert.Equal(ReportStatus.PendingReview, report.Status);

        // Not visible on any public surface before approval.
        Assert.Empty(await h.Reports.GetMapPointsAsync(new PublicReportFilter()));
        Assert.Empty((await h.Reports.GetPublicListAsync(new PublicReportFilter())).Items);
        Assert.Null(await h.Reports.GetPublicDetailAsync(report.Id));

        // Restricted data is stored in the satellite tables only.
        Assert.Equal(SecretNarrative, report.Restricted!.RawNarrative);
        Assert.Equal(SecretContact, report.Restricted.PrivateContactValue);
        Assert.Equal(ExactLat, report.PreciseLocation!.ExactLatitude);
    }

    [Fact]
    public async Task Approved_report_public_surfaces_never_leak_restricted_data()
    {
        using var h = new Harness();
        h.User.AddRole(Roofied.Domain.Identity.RoleNames.Moderator);
        await h.Reports.SubmitAsync(h.SampleInput());
        var report = await h.Db.Reports.SingleAsync();

        var approve = await h.Moderation.ApproveReportAsync(new ApproveReportInput
        {
            ReportId = report.Id,
            PublicSummary = "A person reported feeling unwell after a drink at a downtown venue.",
            SafetyTags = new() { "lost time", "open container" },
        });
        Assert.True(approve.Succeeded);

        // Public detail
        var detail = await h.Reports.GetPublicDetailAsync(report.Id);
        Assert.NotNull(detail);
        AssertNoSecret(System.Text.Json.JsonSerializer.Serialize(detail));

        // Public list
        var list = await h.Reports.GetPublicListAsync(new PublicReportFilter());
        Assert.Single(list.Items);
        AssertNoSecret(System.Text.Json.JsonSerializer.Serialize(list.Items));

        // Public map points use fuzzed coordinates, never the exact ones.
        var points = await h.Reports.GetMapPointsAsync(new PublicReportFilter());
        var point = Assert.Single(points);
        Assert.NotEqual(ExactLat, point.ApproxLatitude);
        Assert.NotEqual(ExactLng, point.ApproxLongitude);
        Assert.True(point.PrecisionMeters >= 500);
        AssertNoSecret(System.Text.Json.JsonSerializer.Serialize(points));
    }

    [Fact]
    public async Task Moderator_view_can_see_restricted_data()
    {
        using var h = new Harness();
        await h.Reports.SubmitAsync(h.SampleInput());
        var report = await h.Db.Reports.SingleAsync();

        var mod = await h.Moderation.GetReportForModerationAsync(report.Id);
        Assert.NotNull(mod);
        Assert.Equal(SecretNarrative, mod!.RawNarrative);
        Assert.Equal(SecretContact, mod.PrivateContactValue);
        Assert.Equal(ExactLat, mod.ExactLatitude);
        // The auto-PII detector should have flagged the narrative for moderator attention.
        Assert.NotEmpty(mod.AutoFlags);
    }

    [Fact]
    public async Task Approved_then_archived_report_leaves_public_surfaces()
    {
        using var h = new Harness();
        await h.Reports.SubmitAsync(h.SampleInput());
        var report = await h.Db.Reports.SingleAsync();
        await h.Moderation.ApproveReportAsync(new ApproveReportInput { ReportId = report.Id, PublicSummary = "ok" });
        Assert.Single((await h.Reports.GetPublicListAsync(new PublicReportFilter())).Items);

        await h.Moderation.ArchiveReportAsync(report.Id, "policy");
        Assert.Empty((await h.Reports.GetPublicListAsync(new PublicReportFilter())).Items);
    }

    private static void AssertNoSecret(string serialized)
    {
        Assert.DoesNotContain("123 Main Street", serialized);
        Assert.DoesNotContain("John Smith", serialized);
        Assert.DoesNotContain("555-987-6543", serialized);
        Assert.DoesNotContain(SecretContact, serialized);
        Assert.DoesNotContain("Dizzy and disoriented", serialized);
        Assert.DoesNotContain("40.71283", serialized);   // exact latitude
        Assert.DoesNotContain("-74.00601", serialized);  // exact longitude
    }
}
