// Roofied map interop (Leaflet). Kept intentionally small: it only renders APPROXIMATE,
// pre-fuzzed points handed to it by the server. It never receives or stores exact coordinates.
window.roofiedMap = (function () {
    const instances = {};

    // Best-effort: after the initial whole-globe view, ask the browser for the user's
    // location and zoom in to their area. If permission is denied or geolocation is
    // unavailable, the globe (or report bounds) view is kept. `onLocated` flags success.
    function geolocate(map, zoom, onLocated) {
        if (!navigator.geolocation) return;
        navigator.geolocation.getCurrentPosition(
            function (pos) {
                map.setView([pos.coords.latitude, pos.coords.longitude], zoom || 11);
                if (typeof onLocated === "function") onLocated();
            },
            function () { /* permission denied or unavailable: keep the default view */ },
            { enableHighAccuracy: false, timeout: 6000, maximumAge: 600000 }
        );
    }

    function init(elementId, options) {
        if (!window.L) {
            console.error("Leaflet not loaded");
            return;
        }
        if (instances[elementId]) {
            try { instances[elementId].map.remove(); } catch (e) { /* ignore */ }
            delete instances[elementId];
        }

        const map = L.map(elementId, {
            scrollWheelZoom: true,
            attributionControl: true,
            worldCopyJump: true,
        }).setView([options.lat ?? 20, options.lng ?? 0], options.zoom ?? 2);

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 18,
            attribution: "&copy; OpenStreetMap contributors",
        }).addTo(map);

        const cluster = L.markerClusterGroup({ showCoverageOnHover: false });
        map.addLayer(cluster);

        instances[elementId] = { map, cluster, hasPoints: false };
        // Leaflet sometimes needs a nudge when rendered inside a flex/hidden container.
        setTimeout(() => map.invalidateSize(), 200);

        // Start on the whole globe, then (after the permission prompt) zoom in to the user's area.
        geolocate(map, 11);
    }

    function setPoints(elementId, points) {
        const inst = instances[elementId];
        if (!inst) return;
        inst.cluster.clearLayers();

        const markers = [];
        (points || []).forEach(p => {
            if (typeof p.lat !== "number" || typeof p.lng !== "number") return;
            const marker = L.marker([p.lat, p.lng]);
            const safeLabel = escapeHtml(p.label || "Approximate area");
            const safeType = escapeHtml(p.type || "Report");
            const href = "/reports/" + encodeURIComponent(p.id);
            marker.bindPopup(
                `<div style="min-width:180px">
                    <strong>${safeType}</strong><br/>
                    <span>${safeLabel}</span><br/>
                    <small>Approximate location &middot; moderated</small><br/>
                    <a href="${href}">View public summary</a>
                 </div>`);
            markers.push(marker);
        });
        inst.cluster.addLayers(markers);
        inst.hasPoints = markers.length > 0;
        // Note: we intentionally do NOT auto-fit to the report bounds. The map starts on the whole
        // globe (all pins visible as clusters); accepting the geolocation prompt zooms to the user's
        // area. This keeps the "globe first, then zoom to me" flow and avoids view jumps.
    }

    function dispose(elementId) {
        const inst = instances[elementId];
        if (inst) {
            try { inst.map.remove(); } catch (e) { /* ignore */ }
            delete instances[elementId];
        }
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }

    // A click-to-pick map for the submission form. Reports the chosen point back to .NET.
    // The chosen point is the user's APPROXIMATE location; the server fuzzes it before publishing.
    function initPicker(elementId, dotnetRef, options) {
        if (!window.L) { console.error("Leaflet not loaded"); return; }
        if (instances[elementId]) {
            try { instances[elementId].map.remove(); } catch (e) { /* ignore */ }
            delete instances[elementId];
        }
        const map = L.map(elementId, { worldCopyJump: true })
            .setView([options.lat ?? 20, options.lng ?? 0], options.zoom ?? 2);
        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 18, attribution: "&copy; OpenStreetMap contributors",
        }).addTo(map);

        let marker = null;
        map.on("click", function (e) {
            const lat = Math.round(e.latlng.lat * 100000) / 100000;
            const lng = Math.round(e.latlng.lng * 100000) / 100000;
            if (marker) { marker.setLatLng(e.latlng); } else { marker = L.marker(e.latlng).addTo(map); }
            if (dotnetRef) { dotnetRef.invokeMethodAsync("HandlePicked", lat, lng); }
        });

        instances[elementId] = { map, cluster: null };
        setTimeout(() => map.invalidateSize(), 200);

        // Start on the whole globe, then zoom in to the user's area after the permission prompt
        // so they can pick a nearby point (no marker auto-placed).
        geolocate(map, 13);
    }

    return { init, setPoints, dispose, initPicker };
})();
