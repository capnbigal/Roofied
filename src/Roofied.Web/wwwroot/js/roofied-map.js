// Roofied map interop (Leaflet). Kept intentionally small: it only renders APPROXIMATE,
// pre-fuzzed points handed to it by the server. It never receives or stores exact coordinates.
window.roofiedMap = (function () {
    const instances = {};

    // Best-effort: after the initial whole-globe view, ask the browser for the user's
    // location and zoom in to their area. If permission is denied or geolocation is
    // unavailable, the globe (or report bounds) view is kept. `onLocated` flags success.
    //
    // Note: mobile browsers only expose geolocation on a SECURE context (HTTPS, or desktop
    // localhost). Over plain http to a LAN IP, navigator.geolocation is blocked on phones.
    function geolocate(map, zoom, onLocated) {
        if (!navigator.geolocation) {
            console.debug("Geolocation API unavailable (insecure context?).");
            return;
        }
        navigator.geolocation.getCurrentPosition(
            function (pos) {
                map.setView([pos.coords.latitude, pos.coords.longitude], zoom || 11);
                if (typeof onLocated === "function") onLocated();
            },
            function (err) {
                // 1 = permission denied, 2 = position unavailable, 3 = timeout
                console.debug("Geolocation unavailable:", err && err.code, err && err.message);
            },
            // Phones often need longer than a few seconds for a first fix.
            { enableHighAccuracy: false, timeout: 15000, maximumAge: 300000 }
        );
    }

    // Adds a tap-friendly "center on my location" button to the map. Useful on mobile and as a
    // retry if the user missed/denied the initial prompt.
    function addLocateControl(map, zoom) {
        if (!window.L || !navigator.geolocation) return;
        const Ctl = L.Control.extend({
            options: { position: "topleft" },
            onAdd: function () {
                const container = L.DomUtil.create("div", "leaflet-bar leaflet-control");
                const link = L.DomUtil.create("a", "", container);
                link.href = "#";
                link.title = "Center on my location";
                link.setAttribute("role", "button");
                link.setAttribute("aria-label", "Center on my location");
                link.innerHTML = "&#x1F4CD;"; // 📍
                link.style.fontSize = "16px";
                link.style.lineHeight = "30px";
                link.style.textAlign = "center";
                L.DomEvent.on(link, "click", function (e) {
                    L.DomEvent.stop(e);
                    geolocate(map, zoom);
                });
                return container;
            },
        });
        map.addControl(new Ctl());
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
        addLocateControl(map, 12);
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
        addLocateControl(map, 14);
        geolocate(map, 13);
    }

    return { init, setPoints, dispose, initPicker };
})();
