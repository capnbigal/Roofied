using Roofied.Application.Common;
using Roofied.Domain.Enums;

namespace Roofied.Application.Resources;

public sealed record ResourceDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public ResourceCategory Category { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Region { get; init; }
    public bool IsEmergency { get; init; }
    public int SortOrder { get; init; }
    public bool IsPublished { get; init; }
}

public sealed class ResourceInput
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ResourceCategory Category { get; set; } = ResourceCategory.General;
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Region { get; set; }
    public bool IsEmergency { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

public interface IResourceService
{
    /// <summary>Published resources for the public Resources page, grouped/ordered for display.</summary>
    Task<IReadOnlyList<ResourceDto>> GetPublishedAsync(string? region = null, CancellationToken ct = default);

    Task<IReadOnlyList<ResourceDto>> GetAllAsync(CancellationToken ct = default);
    Task<ResourceDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<OperationResult<Guid>> UpsertAsync(ResourceInput input, CancellationToken ct = default);
    Task<OperationResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
