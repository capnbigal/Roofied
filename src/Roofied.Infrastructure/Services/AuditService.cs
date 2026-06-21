using System.Text.Json;
using Roofied.Application.Abstractions;
using Roofied.Domain.Audit;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

/// <summary>Persists append-only audit entries. Never store sensitive report text or precise location.</summary>
public sealed class AuditService(RoofiedDbContext db) : IAuditService
{
    public async Task LogAsync(
        string action,
        string? actorUserId = null,
        string? actorDisplayName = null,
        string? entityType = null,
        string? entityId = null,
        string? summary = null,
        object? metadata = null,
        string? ipHash = null,
        CancellationToken ct = default)
    {
        var entry = new AuditLog
        {
            Action = action,
            ActorUserId = actorUserId,
            ActorDisplayName = actorDisplayName,
            EntityType = entityType,
            EntityId = entityId,
            Summary = Truncate(summary, 1000),
            MetadataJson = metadata is null ? null : Truncate(JsonSerializer.Serialize(metadata), 4000),
            IpHash = ipHash,
        };
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : (s.Length <= max ? s : s[..max]);
}
