using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roofied.Domain.Abuse;
using Roofied.Domain.Audit;
using Roofied.Domain.Channels;
using Roofied.Domain.Consent;
using Roofied.Domain.Identity;
using Roofied.Domain.Moderation;
using Roofied.Domain.Profiles;
using Roofied.Domain.Resources;

namespace Roofied.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.HasOne(u => u.Profile)
            .WithOne(p => p.User)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("UserProfiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.DisplayName).HasMaxLength(60);
        b.Property(x => x.Bio).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.UserId).IsUnique();
        // Display names are unique when present (case-insensitive collation handled by SQL Server default).
        b.HasIndex(x => x.DisplayName).IsUnique().HasFilter("[DisplayName] IS NOT NULL");
    }
}

public sealed class ModerationCaseConfiguration : IEntityTypeConfiguration<ModerationCase>
{
    public void Configure(EntityTypeBuilder<ModerationCase> b)
    {
        b.ToTable("ModerationCases");
        b.HasKey(x => x.Id);
        b.Property(x => x.State).HasConversion<int>();
        b.Property(x => x.Priority).HasConversion<int>();
        b.Property(x => x.AssignedToUserId).HasMaxLength(450);
        b.Property(x => x.ResolvedByUserId).HasMaxLength(450);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Report).WithMany(r => r.ModerationCases)
            .HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ChannelPost).WithMany()
            .HasForeignKey(x => x.ChannelPostId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Notes).WithOne(n => n.ModerationCase!)
            .HasForeignKey(n => n.ModerationCaseId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.State);
        b.HasIndex(x => x.ReportId);
        b.HasIndex(x => x.ChannelPostId);
    }
}

public sealed class ModerationNoteConfiguration : IEntityTypeConfiguration<ModerationNote>
{
    public void Configure(EntityTypeBuilder<ModerationNote> b)
    {
        b.ToTable("ModerationNotes");
        b.HasKey(x => x.Id);
        b.Property(x => x.AuthorUserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.Text).IsRequired().HasMaxLength(4000);
        b.HasIndex(x => x.ModerationCaseId);
    }
}

public sealed class ContentFlagConfiguration : IEntityTypeConfiguration<ContentFlag>
{
    public void Configure(EntityTypeBuilder<ContentFlag> b)
    {
        b.ToTable("ContentFlags");
        b.HasKey(x => x.Id);
        b.Property(x => x.ContentType).HasConversion<int>();
        b.Property(x => x.Reason).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Details).HasMaxLength(2000);
        b.Property(x => x.FlaggedByUserId).HasMaxLength(450);
        b.Property(x => x.ReporterIpHash).HasMaxLength(128);
        b.Property(x => x.ResolvedByUserId).HasMaxLength(450);
        b.Property(x => x.ResolutionNote).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.ContentType, x.ContentId });
        b.HasIndex(x => x.Status);
    }
}

public sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> b)
    {
        b.ToTable("Channels");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Guidelines).HasMaxLength(4000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasMany(x => x.Posts).WithOne(p => p.Channel!)
            .HasForeignKey(p => p.ChannelId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ChannelPostConfiguration : IEntityTypeConfiguration<ChannelPost>
{
    public void Configure(EntityTypeBuilder<ChannelPost> b)
    {
        b.ToTable("ChannelPosts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(160);
        b.Property(x => x.Body).IsRequired().HasMaxLength(8000);
        b.Property(x => x.RedactedBody).HasMaxLength(8000);
        b.Property(x => x.AuthorUserId).HasMaxLength(450);
        b.Property(x => x.AuthorDisplayName).HasMaxLength(60);
        b.Property(x => x.AuthorIpHash).HasMaxLength(128);
        b.Property(x => x.ModeratedByUserId).HasMaxLength(450);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasMany(x => x.Comments).WithOne(c => c.ChannelPost!)
            .HasForeignKey(c => c.ChannelPostId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ChannelId, x.Status, x.IsHidden });
        b.HasIndex(x => x.AuthorUserId);
        b.HasIndex(x => x.PublishedUtc);
    }
}

public sealed class ChannelCommentConfiguration : IEntityTypeConfiguration<ChannelComment>
{
    public void Configure(EntityTypeBuilder<ChannelComment> b)
    {
        b.ToTable("ChannelComments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Body).IsRequired().HasMaxLength(4000);
        b.Property(x => x.RedactedBody).HasMaxLength(4000);
        b.Property(x => x.AuthorUserId).HasMaxLength(450);
        b.Property(x => x.AuthorDisplayName).HasMaxLength(60);
        b.Property(x => x.AuthorIpHash).HasMaxLength(128);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.ChannelPostId, x.Status });
    }
}

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> b)
    {
        b.ToTable("Resources");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(160);
        b.Property(x => x.Category).HasConversion<int>();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Url).HasMaxLength(500);
        b.Property(x => x.PhoneNumber).HasMaxLength(60);
        b.Property(x => x.Region).HasMaxLength(120);
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.IsPublished, x.Category, x.SortOrder });
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorUserId).HasMaxLength(450);
        b.Property(x => x.ActorDisplayName).HasMaxLength(120);
        b.Property(x => x.Action).IsRequired().HasMaxLength(120);
        b.Property(x => x.EntityType).HasMaxLength(120);
        b.Property(x => x.EntityId).HasMaxLength(120);
        b.Property(x => x.Summary).HasMaxLength(1000);
        b.Property(x => x.MetadataJson).HasMaxLength(4000);
        b.Property(x => x.IpHash).HasMaxLength(128);
        b.HasIndex(x => x.Action);
        b.HasIndex(x => x.CreatedUtc);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> b)
    {
        b.ToTable("ConsentRecords");
        b.HasKey(x => x.Id);
        b.Property(x => x.ConsentType).IsRequired().HasMaxLength(60);
        b.Property(x => x.ConsentTextVersion).IsRequired().HasMaxLength(40);
        b.Property(x => x.AcknowledgedText).HasMaxLength(4000);
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.IpHash).HasMaxLength(128);
        b.HasIndex(x => x.ReportId);
    }
}

public sealed class AbuseRateLimitEventConfiguration : IEntityTypeConfiguration<AbuseRateLimitEvent>
{
    public void Configure(EntityTypeBuilder<AbuseRateLimitEvent> b)
    {
        b.ToTable("AbuseRateLimitEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<int>();
        b.Property(x => x.ClientKey).IsRequired().HasMaxLength(128);
        b.HasIndex(x => new { x.Action, x.ClientKey, x.OccurredUtc });
    }
}
