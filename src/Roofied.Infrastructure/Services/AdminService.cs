using Microsoft.EntityFrameworkCore;
using Roofied.Application.Abstractions;
using Roofied.Application.Admin;
using Roofied.Application.Common;
using Roofied.Application.Lookups;
using Roofied.Domain.Channels;
using Roofied.Domain.Enums;
using Roofied.Domain.Reports;
using Roofied.Infrastructure.Common;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

public sealed class AdminService(
    IDbContextFactory<RoofiedDbContext> dbFactory,
    IHtmlSanitizer sanitizer,
    ICurrentUser currentUser,
    IAuditService audit) : IAdminService
{
    public async Task<IReadOnlyList<CategoryDto>> GetReportCategoriesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ReportCategories.AsNoTracking().OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.SortOrder, c.IsActive)).ToListAsync(ct);
    }

    public async Task<OperationResult<Guid>> UpsertReportCategoryAsync(CategoryInput input, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        ReportCategory entity;
        if (input.Id is { } id)
            entity = await db.ReportCategories.FirstAsync(c => c.Id == id, ct);
        else
        {
            entity = new ReportCategory { Name = string.Empty, Slug = string.Empty };
            db.ReportCategories.Add(entity);
        }
        entity.Name = sanitizer.SanitizePlainText(input.Name);
        entity.Slug = Slug.From(entity.Name);
        entity.Description = sanitizer.SanitizePlainText(input.Description);
        entity.SortOrder = input.SortOrder;
        entity.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ReportCategory.Upserted", currentUser.UserId, currentUser.DisplayName, nameof(ReportCategory), entity.Id.ToString(), ct: ct);
        return OperationResult<Guid>.Success(entity.Id);
    }

    public async Task<OperationResult> DeleteReportCategoryAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.Reports.AnyAsync(r => r.ReportCategoryId == id, ct))
            return OperationResult.Fail("Cannot delete a category that is in use. Deactivate it instead.");
        var entity = await db.ReportCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return OperationResult.Fail("Not found.");
        db.ReportCategories.Remove(entity);
        await db.SaveChangesAsync(ct);
        return OperationResult.Success();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetVenueCategoriesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.VenueCategories.AsNoTracking().OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.SortOrder, c.IsActive)).ToListAsync(ct);
    }

    public async Task<OperationResult<Guid>> UpsertVenueCategoryAsync(CategoryInput input, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        VenueCategory entity;
        if (input.Id is { } id)
            entity = await db.VenueCategories.FirstAsync(c => c.Id == id, ct);
        else
        {
            entity = new VenueCategory { Name = string.Empty, Slug = string.Empty };
            db.VenueCategories.Add(entity);
        }
        entity.Name = sanitizer.SanitizePlainText(input.Name);
        entity.Slug = Slug.From(entity.Name);
        entity.Description = sanitizer.SanitizePlainText(input.Description);
        entity.SortOrder = input.SortOrder;
        entity.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("VenueCategory.Upserted", currentUser.UserId, currentUser.DisplayName, nameof(VenueCategory), entity.Id.ToString(), ct: ct);
        return OperationResult<Guid>.Success(entity.Id);
    }

    public async Task<OperationResult> DeleteVenueCategoryAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.Reports.AnyAsync(r => r.VenueCategoryId == id, ct))
            return OperationResult.Fail("Cannot delete a category that is in use. Deactivate it instead.");
        var entity = await db.VenueCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return OperationResult.Fail("Not found.");
        db.VenueCategories.Remove(entity);
        await db.SaveChangesAsync(ct);
        return OperationResult.Success();
    }

    public async Task<IReadOnlyList<AdminChannelDto>> GetChannelsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Channels.AsNoTracking().OrderBy(c => c.SortOrder)
            .Select(c => new AdminChannelDto(c.Id, c.Name, c.Slug, c.Description, c.Guidelines,
                c.SortOrder, c.IsActive, c.IsLocked, c.AllowAnonymousPosts, c.CommentsEnabled)).ToListAsync(ct);
    }

    public async Task<OperationResult<Guid>> UpsertChannelAsync(ChannelInput input, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        Channel entity;
        if (input.Id is { } id)
            entity = await db.Channels.FirstAsync(c => c.Id == id, ct);
        else
        {
            entity = new Channel { Name = string.Empty, Slug = string.Empty };
            db.Channels.Add(entity);
        }
        entity.Name = sanitizer.SanitizePlainText(input.Name);
        entity.Slug = Slug.From(entity.Name);
        entity.Description = sanitizer.SanitizePlainText(input.Description);
        entity.Guidelines = sanitizer.SanitizeHtml(input.Guidelines);
        entity.SortOrder = input.SortOrder;
        entity.IsActive = input.IsActive;
        entity.IsLocked = input.IsLocked;
        entity.AllowAnonymousPosts = input.AllowAnonymousPosts;
        entity.CommentsEnabled = input.CommentsEnabled;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Channel.Upserted", currentUser.UserId, currentUser.DisplayName, nameof(Channel), entity.Id.ToString(), ct: ct);
        return OperationResult<Guid>.Success(entity.Id);
    }

    public async Task<OperationResult> SetChannelLockedAsync(Guid id, bool locked, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Channels.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return OperationResult.Fail("Not found.");
        entity.IsLocked = locked;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Channel.LockChanged", currentUser.UserId, currentUser.DisplayName, nameof(Channel), id.ToString(), $"Locked={locked}", ct: ct);
        return OperationResult.Success();
    }

    public async Task<DashboardMetrics> GetMetricsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return new DashboardMetrics(
            PendingReports: await db.Reports.CountAsync(r => r.Status == ReportStatus.PendingReview, ct),
            ApprovedReports: await db.Reports.CountAsync(r => r.Status == ReportStatus.Approved, ct),
            PendingPosts: await db.ChannelPosts.CountAsync(p => p.Status == ChannelPostStatus.PendingReview, ct),
            OpenFlags: await db.ContentFlags.CountAsync(f => f.Status == FlagStatus.Open, ct),
            TotalUsers: await db.Users.CountAsync(ct),
            ResourcesPublished: await db.Resources.CountAsync(r => r.IsPublished, ct));
    }
}
