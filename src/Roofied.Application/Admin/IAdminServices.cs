using Roofied.Application.Common;
using Roofied.Application.Lookups;

namespace Roofied.Application.Admin;

public sealed class CategoryInput
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ChannelInput
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Guidelines { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; }
    public bool AllowAnonymousPosts { get; set; } = true;
    public bool CommentsEnabled { get; set; }
}

public sealed record AdminChannelDto(
    Guid Id, string Name, string Slug, string? Description, string? Guidelines,
    int SortOrder, bool IsActive, bool IsLocked, bool AllowAnonymousPosts, bool CommentsEnabled);

public sealed record DashboardMetrics(
    int PendingReports,
    int ApprovedReports,
    int PendingPosts,
    int OpenFlags,
    int TotalUsers,
    int ResourcesPublished);

/// <summary>Administrative management of categories, channels, and platform metrics.</summary>
public interface IAdminService
{
    // Report categories
    Task<IReadOnlyList<CategoryDto>> GetReportCategoriesAsync(CancellationToken ct = default);
    Task<OperationResult<Guid>> UpsertReportCategoryAsync(CategoryInput input, CancellationToken ct = default);
    Task<OperationResult> DeleteReportCategoryAsync(Guid id, CancellationToken ct = default);

    // Venue categories
    Task<IReadOnlyList<CategoryDto>> GetVenueCategoriesAsync(CancellationToken ct = default);
    Task<OperationResult<Guid>> UpsertVenueCategoryAsync(CategoryInput input, CancellationToken ct = default);
    Task<OperationResult> DeleteVenueCategoryAsync(Guid id, CancellationToken ct = default);

    // Channels
    Task<IReadOnlyList<AdminChannelDto>> GetChannelsAsync(CancellationToken ct = default);
    Task<OperationResult<Guid>> UpsertChannelAsync(ChannelInput input, CancellationToken ct = default);
    Task<OperationResult> SetChannelLockedAsync(Guid id, bool locked, CancellationToken ct = default);

    // Metrics
    Task<DashboardMetrics> GetMetricsAsync(CancellationToken ct = default);
}

public sealed record AdminUserDto(
    string Id, string? Email, string? DisplayName, bool IsDisabled,
    IReadOnlyList<string> Roles, DateTime CreatedUtc, DateTime? LastSignInUtc);

/// <summary>Administrative management of users and roles.</summary>
public interface IUserAdminService
{
    Task<PagedResult<AdminUserDto>> GetUsersAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<OperationResult> SetRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct = default);
    Task<OperationResult> SetDisabledAsync(string userId, bool disabled, CancellationToken ct = default);
}
