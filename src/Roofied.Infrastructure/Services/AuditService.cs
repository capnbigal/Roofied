using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roofied.Application.Abstractions;
using Roofied.Domain.Audit;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

/// <summary>Persists append-only audit entries. Never store sensitive report text or precise location.</summary>
public sealed class AuditService(IDbContextFactory<RoofiedDbContext> dbFactory) : IAuditService
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
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            ActorUserId = actorUserId,
            ActorDisplayName = actorDisplayName,
            EntityType = entityType,
            EntityId = entityId,
            Summary = Truncate(summary, 1000),
            MetadataJson = metadata is null ? null : Truncate(JsonSerializer.Serialize(metadata), 4000),
            IpHash = ipHash,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : (s.Length <= max ? s : s[..max]);
}
