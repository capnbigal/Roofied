using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roofied.Domain.Reports;

namespace Roofied.Infrastructure.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.ToTable("Reports");
        b.HasKey(x => x.Id);

        b.Property(x => x.ReferenceCode).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.ReferenceCode).IsUnique();

        b.Property(x => x.City).IsRequired().HasMaxLength(120);
        b.Property(x => x.Region).HasMaxLength(120);
        b.Property(x => x.Country).HasMaxLength(120);
        b.Property(x => x.PublicSummary).HasMaxLength(4000);
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);

        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Visibility).HasConversion<int>();
        b.Property(x => x.SuspicionLevel).HasConversion<int>();

        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.ReportCategory)
            .WithMany(c => c.Reports)
            .HasForeignKey(x => x.ReportCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.VenueCategory)
            .WithMany(c => c.Reports)
            .HasForeignKey(x => x.VenueCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Restricted)
            .WithOne(r => r.Report)
            .HasForeignKey<ReportRestricted>(r => r.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.PreciseLocation)
            .WithOne(r => r.Report)
            .HasForeignKey<ReportLocation>(r => r.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.PublicLocation)
            .WithOne(r => r.Report)
            .HasForeignKey<ReportPublicLocation>(r => r.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.SafetyTags).WithOne(t => t.Report!).HasForeignKey(t => t.ReportId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.StatusHistory).WithOne(t => t.Report!).HasForeignKey(t => t.ReportId).OnDelete(DeleteBehavior.Cascade);

        // Composite index optimized for the public surfaces query.
        b.HasIndex(x => new { x.Status, x.Visibility, x.IsDeleted });
        b.HasIndex(x => x.PublishedUtc);
        b.HasIndex(x => x.City);
        b.HasIndex(x => x.IncidentDateFrom);
        b.HasIndex(x => x.CreatedByUserId);
    }
}

public sealed class ReportRestrictedConfiguration : IEntityTypeConfiguration<ReportRestricted>
{
    public void Configure(EntityTypeBuilder<ReportRestricted> b)
    {
        b.ToTable("ReportRestricted");
        b.HasKey(x => x.ReportId);
        b.Property(x => x.RawNarrative).HasMaxLength(8000);
        b.Property(x => x.RedactedNarrative).HasMaxLength(8000);
        b.Property(x => x.SymptomsDescription).HasMaxLength(4000);
        b.Property(x => x.PrivateContactMethod).HasMaxLength(64);
        b.Property(x => x.PrivateContactValue).HasMaxLength(256);
        b.Property(x => x.SubmitterIpHash).HasMaxLength(128);
        b.Property(x => x.AutoFlagsJson).HasMaxLength(4000);
    }
}

public sealed class ReportLocationConfiguration : IEntityTypeConfiguration<ReportLocation>
{
    public void Configure(EntityTypeBuilder<ReportLocation> b)
    {
        b.ToTable("ReportLocations");
        b.HasKey(x => x.ReportId);
        b.Property(x => x.ExactAddress).HasMaxLength(500);
        b.Property(x => x.LocationNotes).HasMaxLength(1000);
    }
}

public sealed class ReportPublicLocationConfiguration : IEntityTypeConfiguration<ReportPublicLocation>
{
    public void Configure(EntityTypeBuilder<ReportPublicLocation> b)
    {
        b.ToTable("ReportPublicLocations");
        b.HasKey(x => x.ReportId);
        b.Property(x => x.GeneralizedAreaLabel).HasMaxLength(200);
        b.Property(x => x.GridCellKey).HasMaxLength(64);
        b.HasIndex(x => x.GridCellKey);
    }
}

public sealed class ReportSafetyTagConfiguration : IEntityTypeConfiguration<ReportSafetyTag>
{
    public void Configure(EntityTypeBuilder<ReportSafetyTag> b)
    {
        b.ToTable("ReportSafetyTags");
        b.HasKey(x => x.Id);
        b.Property(x => x.Label).IsRequired().HasMaxLength(60);
        b.HasIndex(x => x.ReportId);
    }
}

public sealed class ReportStatusHistoryConfiguration : IEntityTypeConfiguration<ReportStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReportStatusHistory> b)
    {
        b.ToTable("ReportStatusHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromStatus).HasConversion<int?>();
        b.Property(x => x.ToStatus).HasConversion<int>();
        b.Property(x => x.ChangedByUserId).HasMaxLength(450);
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasIndex(x => x.ReportId);
    }
}

public sealed class ReportCategoryConfiguration : IEntityTypeConfiguration<ReportCategory>
{
    public void Configure(EntityTypeBuilder<ReportCategory> b)
    {
        b.ToTable("ReportCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public sealed class VenueCategoryConfiguration : IEntityTypeConfiguration<VenueCategory>
{
    public void Configure(EntityTypeBuilder<VenueCategory> b)
    {
        b.ToTable("VenueCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}
