// Leaflet + OpenStreetMap wrapper used by the LeafletMap Blazor component.
// Leaflet is loaded lazily from a CDN the first time a map is created (no API key needed).
const LEAFLET_VERSION = "1.9.4";
const maps = new Map();
let leafletLoading = null;

function loadLeaflet() {
  if (window.L) return Promise.resolve();
  if (leafletLoading) return leafletLoading;
  leafletLoading = new Promise((resolve, reject) => {
    const css = document.createElement("link");
    css.rel = "stylesheet";
    css.href = `https://cdn.jsdelivr.net/npm/leaflet@${LEAFLET_VERSION}/dist/leaflet.css`;
    document.head.appendChild(css);
    const script = document.createElement("script");
    script.src = `https://cdn.jsdelivr.net/npm/leaflet@${LEAFLET_VERSION}/dist/leaflet.js`;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("Leaflet could not be loaded"));
    document.head.appendChild(script);
  });
  return leafletLoading;
}

const COLORS = { stop: "#1976d2", start: "#2e7d32", end: "#c62828", done: "#757575", failed: "#ef6c00", pin: "#6a1b9a" };

function markerIcon(marker) {
  const color = COLORS[marker.kind] ?? COLORS.stop;
  const text = marker.number ?? (marker.kind === "start" ? "S" : marker.kind === "end" ? "K" : "");
  return L.divIcon({
    className: "",
    html: `<div style="background:${color};color:#fff;border:2px solid #fff;border-radius:50%;width:28px;height:28px;display:flex;align-items:center;justify-content:center;font:600 13px system-ui;box-shadow:0 1px 4px rgba(0,0,0,.4)">${text}</div>`,
    iconSize: [28, 28],
    iconAnchor: [14, 14],
  });
}

export async function create(elementId, dotNetRef, center, zoom, pickEnabled) {
  await loadLeaflet();
  const element = document.getElementById(elementId);
  if (!element) return;
  const map = L.map(element).setView([center.latitude, center.longitude], zoom);
  L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  }).addTo(map);
  const layer = L.layerGroup().addTo(map);
  if (pickEnabled) {
    map.on("click", (e) => dotNetRef.invokeMethodAsync("OnMapClicked", e.latlng.lat, e.latlng.lng));
  }
  maps.set(elementId, { map, layer });
  // Maps inside dialogs/tabs are created before they are visible: recompute size once shown.
  setTimeout(() => map.invalidateSize(), 250);
}

export function update(elementId, markers, route, fit) {
  const entry = maps.get(elementId);
  if (!entry) return;
  const { map, layer } = entry;
  layer.clearLayers();
  const bounds = [];
  if (route && route.length > 1) {
    const line = route.map((p) => [p.latitude, p.longitude]);
    L.polyline(line, { color: "#1976d2", weight: 4, opacity: 0.7 }).addTo(layer);
    bounds.push(...line);
  }
  for (const m of markers ?? []) {
    const leafletMarker = L.marker([m.latitude, m.longitude], { icon: markerIcon(m) }).addTo(layer);
    if (m.label) leafletMarker.bindTooltip(m.label);
    bounds.push([m.latitude, m.longitude]);
  }
  if (fit && bounds.length > 1) {
    map.fitBounds(bounds, { padding: [30, 30], maxZoom: 16 });
  } else if (fit && bounds.length === 1) {
    map.setView(bounds[0], Math.max(map.getZoom(), 15));
  }
}

export function dispose(elementId) {
  const entry = maps.get(elementId);
  if (entry) {
    entry.map.remove();
    maps.delete(elementId);
  }
}
