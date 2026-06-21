namespace Roofied.Domain.Common;

/// <summary>Tracks creation/update timestamps in UTC. Set centrally in the DbContext.</summary>
public interface IAuditableEntity
{
    DateTime CreatedUtc { get; set; }
    DateTime? UpdatedUtc { get; set; }
}

/// <summary>Marks an entity as soft-deletable. A global query filter hides deleted rows.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedUtc { get; set; }
}

/// <summary>Optimistic-concurrency token.</summary>
public interface IHasRowVersion
{
    byte[]? RowVersion { get; set; }
}

/// <summary>Base for entities with a GUID key and audit timestamps.</summary>
public abstract class AuditableEntity : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

/// <summary>Base for auditable + soft-deletable + concurrency-aware aggregate roots.</summary>
public abstract class FullAuditableEntity : AuditableEntity, ISoftDeletable, IHasRowVersion
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedUtc { get; set; }
    public byte[]? RowVersion { get; set; }
}
