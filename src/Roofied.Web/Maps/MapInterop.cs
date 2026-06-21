using Microsoft.JSInterop;
using Roofied.Application.Reports.Dtos;

namespace Roofied.Web.Maps;

/// <summary>A public-safe point handed to the client map. Carries only fuzzed coordinates + labels.</summary>
public sealed record MapMarker(string Id, double Lat, double Lng, string Label, string Type);

/// <summary>
/// Abstraction over the client map so the provider (currently Leaflet/OSM) can be swapped without
/// touching components. Implementations call a matching JS module.
/// </summary>
public interface IMapInterop
{
    Task InitAsync(string elementId, double centerLat, double centerLng, int zoom);
    Task SetPointsAsync(string elementId, IEnumerable<MapMarker> markers);
    Task DisposeMapAsync(string elementId);
}

/// <summary>Leaflet-backed implementation that talks to window.roofiedMap in roofied-map.js.</summary>
public sealed class LeafletMapInterop(IJSRuntime js) : IMapInterop
{
    public async Task InitAsync(string elementId, double centerLat, double centerLng, int zoom) =>
        await js.InvokeVoidAsync("roofiedMap.init", elementId, new { lat = centerLat, lng = centerLng, zoom });

    public async Task SetPointsAsync(string elementId, IEnumerable<MapMarker> markers) =>
        await js.InvokeVoidAsync("roofiedMap.setPoints", elementId,
            markers.Select(m => new { id = m.Id, lat = m.Lat, lng = m.Lng, label = m.Label, type = m.Type }));

    public async Task DisposeMapAsync(string elementId)
    {
        try { await js.InvokeVoidAsync("roofiedMap.dispose", elementId); }
        catch (JSDisconnectedException) { /* circuit gone; nothing to clean up */ }
    }

    public static MapMarker FromPoint(PublicMapPointDto p) =>
        new(p.Id.ToString(), p.ApproxLatitude, p.ApproxLongitude,
            p.GeneralizedAreaLabel ?? "Approximate area", p.IncidentTypeName ?? "Report");
}
