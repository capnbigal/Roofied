namespace Roofied.Application.Lookups;

public sealed record CategoryDto(Guid Id, string Name, string Slug, string? Description, int SortOrder, bool IsActive);

/// <summary>Read-only lookups used by submission forms and public filters.</summary>
public interface ILookupService
{
    Task<IReadOnlyList<CategoryDto>> GetReportCategoriesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>> GetVenueCategoriesAsync(bool activeOnly = true, CancellationToken ct = default);
}
