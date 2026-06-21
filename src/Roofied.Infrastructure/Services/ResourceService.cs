using Microsoft.EntityFrameworkCore;
using Roofied.Application.Abstractions;
using Roofied.Application.Common;
using Roofied.Application.Resources;
using Roofied.Domain.Resources;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

public sealed class ResourceService(
    IDbContextFactory<RoofiedDbContext> dbFactory,
    IHtmlSanitizer sanitizer,
    ICurrentUser currentUser,
    IAuditService audit) : IResourceService
{
    public async Task<IReadOnlyList<ResourceDto>> GetPublishedAsync(string? region = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Resources.AsNoTracking().Where(r => r.IsPublished);
        if (!string.IsNullOrWhiteSpace(region))
            query = query.Where(r => r.Region == null || r.Region == region);
        return await query
            .OrderByDescending(r => r.IsEmergency)
            .ThenBy(r => r.Category).ThenBy(r => r.SortOrder).ThenBy(r => r.Title)
            .Select(Project()).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ResourceDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Resources.AsNoTracking()
            .OrderBy(r => r.Category).ThenBy(r => r.SortOrder).ThenBy(r => r.Title)
            .Select(Project()).ToListAsync(ct);
    }

    public async Task<ResourceDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Resources.AsNoTracking().Where(r => r.Id == id).Select(Project()).FirstOrDefaultAsync(ct);
    }

    public async Task<OperationResult<Guid>> UpsertAsync(ResourceInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
            return OperationResult<Guid>.Fail("Title is required.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        Resource entity;
        if (input.Id is { } id)
        {
            entity = await db.Resources.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new InvalidOperationException("Resource not found.");
        }
        else
        {
            entity = new Resource { Title = string.Empty, CreatedByUserId = currentUser.UserId };
            db.Resources.Add(entity);
        }

        entity.Title = sanitizer.SanitizePlainText(input.Title);
        entity.Category = input.Category;
        entity.Description = sanitizer.SanitizeHtml(input.Description);
        entity.Url = string.IsNullOrWhiteSpace(input.Url) ? null : input.Url.Trim();
        entity.PhoneNumber = sanitizer.SanitizePlainText(input.PhoneNumber);
        entity.Region = sanitizer.SanitizePlainText(input.Region);
        entity.IsEmergency = input.IsEmergency;
        entity.SortOrder = input.SortOrder;
        entity.IsPublished = input.IsPublished;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Resource.Upserted", currentUser.UserId, currentUser.DisplayName, nameof(Resource),
            entity.Id.ToString(), $"Resource '{entity.Title}' saved.", ipHash: currentUser.IpHash, ct: ct);
        return OperationResult<Guid>.Success(entity.Id);
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Resources.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return OperationResult.Fail("Resource not found.");
        db.Resources.Remove(entity); // soft-deleted via SaveChanges interceptor
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Resource.Deleted", currentUser.UserId, currentUser.DisplayName, nameof(Resource),
            id.ToString(), ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    private static System.Linq.Expressions.Expression<Func<Resource, ResourceDto>> Project() => r => new ResourceDto
    {
        Id = r.Id,
        Title = r.Title,
        Category = r.Category,
        Description = r.Description,
        Url = r.Url,
        PhoneNumber = r.PhoneNumber,
        Region = r.Region,
        IsEmergency = r.IsEmergency,
        SortOrder = r.SortOrder,
        IsPublished = r.IsPublished,
    };
}
