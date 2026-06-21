using Roofied.Application.Geo;

namespace Roofied.Tests;

public class LocationPrecisionServiceTests
{
    private readonly LocationPrecisionService _svc = new();

    [Fact]
    public void Fuzz_is_deterministic_for_same_input()
    {
        var opts = new LocationPrecisionOptions { GridSizeMeters = 1500, MinGridSizeMeters = 500 };
        var a = _svc.Fuzz(40.7128, -74.0060, opts);
        var b = _svc.Fuzz(40.7128, -74.0060, opts);

        Assert.Equal(a.ApproxLatitude, b.ApproxLatitude);
        Assert.Equal(a.ApproxLongitude, b.ApproxLongitude);
        Assert.Equal(a.GridCellKey, b.GridCellKey);
    }

    [Fact]
    public void Fuzz_moves_the_point_away_from_the_exact_location()
    {
        var opts = new LocationPrecisionOptions { GridSizeMeters = 1500, MinGridSizeMeters = 500 };
        var result = _svc.Fuzz(40.7128, -74.0060, opts);

        // The published point should not equal the exact input (it is snapped to a cell centroid).
        Assert.NotEqual(40.7128, result.ApproxLatitude);
        Assert.NotEqual(-74.0060, result.ApproxLongitude);
    }

    [Fact]
    public void Points_within_the_same_grid_cell_share_a_public_point_and_key()
    {
        const int grid = 2000;
        var opts = new LocationPrecisionOptions { GridSizeMeters = grid, MinGridSizeMeters = 500 };

        // Construct two points guaranteed to fall inside the same cell (no boundary straddle).
        const double metersPerDegLat = 111_320d;
        var latStep = grid / metersPerDegLat;
        var baseLat = Math.Floor(40.7128 / latStep) * latStep;
        var lonStep = grid / (metersPerDegLat * Math.Cos(40.7128 * Math.PI / 180d));
        var baseLon = Math.Floor(-74.0060 / lonStep) * lonStep;

        // Keep latitude identical (longitude grid depends on latitude) and vary only longitude.
        var lat = baseLat + latStep * 0.3;
        var a = _svc.Fuzz(lat, baseLon + lonStep * 0.1, opts);
        var b = _svc.Fuzz(lat, baseLon + lonStep * 0.4, opts);

        Assert.Equal(a.GridCellKey, b.GridCellKey);
        Assert.Equal(a.ApproxLatitude, b.ApproxLatitude);
        Assert.Equal(a.ApproxLongitude, b.ApproxLongitude);
    }

    [Fact]
    public void Published_point_stays_within_one_grid_cell_of_the_exact_point()
    {
        const int grid = 1500;
        var opts = new LocationPrecisionOptions { GridSizeMeters = grid, MinGridSizeMeters = 500 };
        var result = _svc.Fuzz(40.7128, -74.0060, opts);

        var latDegTolerance = grid / 111_320d; // one cell
        Assert.True(Math.Abs(result.ApproxLatitude - 40.7128) <= latDegTolerance);
    }

    [Fact]
    public void Safety_floor_prevents_a_too_precise_grid()
    {
        var opts = new LocationPrecisionOptions { GridSizeMeters = 50, MinGridSizeMeters = 500 };
        var result = _svc.Fuzz(40.7128, -74.0060, opts);

        Assert.Equal(500, result.PrecisionMeters);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    public void Fuzz_rejects_out_of_range_coordinates(double lat, double lon)
    {
        var opts = new LocationPrecisionOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => _svc.Fuzz(lat, lon, opts));
    }
}
