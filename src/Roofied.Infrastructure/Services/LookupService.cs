using Microsoft.EntityFrameworkCore;
using Roofied.Application.Lookups;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

public sealed class LookupService(IDbContextFactory<RoofiedDbContext> dbFactory) : ILookupService
{
    public async Task<IReadOnlyList<CategoryDto>> GetReportCategoriesAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.ReportCategories.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.SortOrder, c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetVenueCategoriesAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.VenueCategories.AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.SortOrder, c.IsActive))
            .ToListAsync(ct);
    }
}
