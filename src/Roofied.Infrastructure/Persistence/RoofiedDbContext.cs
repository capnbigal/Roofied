using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Roofied.Application.Abstractions;
using Roofied.Domain.Abuse;
using Roofied.Domain.Audit;
using Roofied.Domain.Channels;
using Roofied.Domain.Common;
using Roofied.Domain.Consent;
using Roofied.Domain.Identity;
using Roofied.Domain.Moderation;
using Roofied.Domain.Profiles;
using Roofied.Domain.Reports;
using Roofied.Domain.Resources;

namespace Roofied.Infrastructure.Persistence;

/// <summary>
/// Application database context. Inherits Identity schema and adds all domain aggregates.
/// Centralizes audit-timestamp stamping and soft-delete handling in SaveChanges, and applies a
/// global query filter so soft-deleted rows are excluded from ordinary queries.
/// </summary>
public class RoofiedDbContext(DbContextOptions<RoofiedDbContext> options, IClock clock)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportRestricted> ReportRestricted => Set<ReportRestricted>();
    public DbSet<ReportLocation> ReportLocations => Set<ReportLocation>();
    public DbSet<ReportPublicLocation> ReportPublicLocations => Set<ReportPublicLocation>();
    public DbSet<ReportSafetyTag> ReportSafetyTags => Set<ReportSafetyTag>();
    public DbSet<ReportStatusHistory> ReportStatusHistory => Set<ReportStatusHistory>();
    public DbSet<ReportCategory> ReportCategories => Set<ReportCategory>();
    public DbSet<VenueCategory> VenueCategories => Set<VenueCategory>();
    public DbSet<ModerationCase> ModerationCases => Set<ModerationCase>();
    public DbSet<ModerationNote> ModerationNotes => Set<ModerationNote>();
    public DbSet<ContentFlag> ContentFlags => Set<ContentFlag>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChannelPost> ChannelPosts => Set<ChannelPost>();
    public DbSet<ChannelComment> ChannelComments => Set<ChannelComment>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<AbuseRateLimitEvent> AbuseRateLimitEvents => Set<AbuseRateLimitEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(RoofiedDbContext).Assembly);

        // Apply a soft-delete query filter to every ISoftDeletable entity.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(prop), parameter);
                entityType.SetQueryFilter(filter);
            }
        }

        // The in-memory provider (used in tests) does not maintain SQL Server rowversion tokens,
        // which would otherwise cause spurious concurrency failures. Disable the tokens there only;
        // production (SQL Server) keeps full optimistic concurrency.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties().Where(p => p.IsConcurrencyToken))
                {
                    property.IsConcurrencyToken = false;
                    property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                }
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditRules()
    {
        var now = clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    if (auditable.CreatedUtc == default)
                        auditable.CreatedUtc = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedUtc = now;
                }
            }

            // Convert hard deletes of soft-deletable entities into soft deletes.
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable soft)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedUtc = now;
            }
        }
    }
}
