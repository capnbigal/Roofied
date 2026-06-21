using Roofied.Domain.Common;

namespace Roofied.Domain.Reports;

/// <summary>Admin-managed incident type (e.g. "Drink tampering", "Needle spiking").</summary>
public class ReportCategory : FullAuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Report> Reports { get; set; } = new List<Report>();
}

/// <summary>Admin-managed generalized venue category (bar, restaurant, party, concert, home, rideshare, other).</summary>
public class VenueCategory : FullAuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
