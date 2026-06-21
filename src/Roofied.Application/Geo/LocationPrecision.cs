namespace Roofied.Application.Geo;

/// <summary>Resolved precision settings used when fuzzing a single report's location.</summary>
public sealed record LocationPrecisionOptions
{
    /// <summary>Edge length of the generalization grid cell, in meters.</summary>
    public int GridSizeMeters { get; init; } = 1500;

    /// <summary>
    /// Hard safety floor. Even if an admin misconfigures a smaller value, the service will never
    /// publish a grid finer than this, to avoid revealing near-exact locations.
    /// </summary>
    public int MinGridSizeMeters { get; init; } = 500;
}

/// <summary>Public-safe, fuzzed location result.</summary>
public sealed record FuzzedLocation
{
    public required double ApproxLatitude { get; init; }
    public required double ApproxLongitude { get; init; }
    public required int PrecisionMeters { get; init; }
    public required string GridCellKey { get; init; }
}

/// <summary>
/// Converts an exact coordinate into an intentionally imprecise public coordinate by snapping it to
/// the centroid of a fixed grid cell. Nearby reports share a cell (good for clustering and pattern
/// display) and the exact within-cell position is discarded, so the original point cannot be recovered.
/// </summary>
public interface ILocationPrecisionService
{
    FuzzedLocation Fuzz(double exactLatitude, double exactLongitude, LocationPrecisionOptions options);
}

public sealed class LocationPrecisionService : ILocationPrecisionService
{
    private const double MetersPerDegreeLatitude = 111_320d;

    public FuzzedLocation Fuzz(double exactLatitude, double exactLongitude, LocationPrecisionOptions options)
    {
        if (exactLatitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(exactLatitude));
        if (exactLongitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(exactLongitude));

        // Enforce the safety floor: never publish a grid finer than the configured minimum.
        var gridMeters = Math.Max(options.GridSizeMeters, options.MinGridSizeMeters);

        var latStepDeg = gridMeters / MetersPerDegreeLatitude;

        // Longitude degrees shrink with latitude; guard the poles to avoid divide-by-zero.
        var cosLat = Math.Cos(exactLatitude * Math.PI / 180d);
        var lonMetersPerDegree = Math.Max(MetersPerDegreeLatitude * Math.Abs(cosLat), 1d);
        var lonStepDeg = gridMeters / lonMetersPerDegree;

        var latIndex = (long)Math.Floor(exactLatitude / latStepDeg);
        var lonIndex = (long)Math.Floor(exactLongitude / lonStepDeg);

        // Snap to the cell centroid. The exact within-cell offset is intentionally thrown away.
        var approxLat = (latIndex + 0.5) * latStepDeg;
        var approxLon = (lonIndex + 0.5) * lonStepDeg;

        // Keep results in valid ranges.
        approxLat = Math.Clamp(approxLat, -90d, 90d);
        approxLon = Math.Clamp(approxLon, -180d, 180d);

        return new FuzzedLocation
        {
            ApproxLatitude = Math.Round(approxLat, 5),
            ApproxLongitude = Math.Round(approxLon, 5),
            PrecisionMeters = gridMeters,
            GridCellKey = $"{gridMeters}:{latIndex}:{lonIndex}",
        };
    }
}
