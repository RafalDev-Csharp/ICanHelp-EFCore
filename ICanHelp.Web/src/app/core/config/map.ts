import type { GeoPoint } from '../geo/geo-point';

/**
 * Kafelki mapy. Na razie OpenStreetMap (darmowe, bez klucza, ale z limitami – tylko do pracy i testów).
 * Przed wdrożeniem (F10) – dostawca kafelków z konfiguracji (np. MapTiler).
 */
export const MAP_TILES = {
  url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
  attribution:
    '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a>',
  maxZoom: 19,
} as const;

/** Środek mapy, gdy nie ma pinezek ani pozycji użytkownika: Augustów (MVP – test w jednym mieście). */
export const DEFAULT_MAP_CENTER: GeoPoint = { latitude: 53.8437, longitude: 22.9785 };
export const DEFAULT_MAP_ZOOM = 12;

/** Link „Otwórz w mapach” – pełna mapa OSM z pinezką. */
export function openStreetMapUrl(point: GeoPoint, zoom = 16): string {
  const { latitude: lat, longitude: lon } = point;
  return `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lon}#map=${zoom}/${lat}/${lon}`;
}
