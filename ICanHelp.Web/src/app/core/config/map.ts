import type { GeoPoint } from '../geo/geo-point';

/**
 * Kafelki mapy. Na razie OpenStreetMap (darmowe, bez klucza, ale z limitami – tylko do pracy i testów).
 * Przed wdrożeniem (F10) – dostawca kafelków z konfiguracji (np. MapTiler).
 */
export const MAP_TILES = {
  url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
  attribution: '<a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">&copy; OSM</a>',
  maxZoom: 19,
} as const;

/** Środek mapy, gdy nie ma pinezek ani pozycji użytkownika: środek Polski, oddalony – widać prawie cały kraj. */
export const DEFAULT_MAP_CENTER: GeoPoint = { latitude: 52.07, longitude: 19.48 };
export const DEFAULT_MAP_ZOOM = 6;
/** Zbliżenie na jeden punkt (`center` bez własnego `zoom`). */
export const POINT_MAP_ZOOM = 15;

/** Link „Otwórz w mapach” – pełna mapa OSM z pinezką. */
export function openStreetMapUrl(point: GeoPoint, zoom = 16): string {
  const { latitude: lat, longitude: lon } = point;
  return `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lon}#map=${zoom}/${lat}/${lon}`;
}

/**
 * Wyszukiwanie adresu: Nominatim (OpenStreetMap). Zasady użycia: najwyżej 1 zapytanie na sekundę, bez podpowiedzi
 * „w trakcie pisania” – szukamy dopiero po zatwierdzeniu. Przy większym ruchu (F10) – własna instancja albo płatny dostawca.
 */
export const GEOCODING = {
  url: 'https://nominatim.openstreetmap.org/search',
  /** Kraje, w których szukamy (MVP: Polska). */
  countryCodes: 'pl',
  limit: 5,
  minIntervalMs: 1_100,
} as const;

/**
 * Podpowiedzi adresu w trakcie pisania: Photon (komoot, dane OSM) – w przeciwieństwie do Nominatim jest do tego
 * przeznaczony. Darmowy, bez klucza, „fair use” – dlatego dopiero od 3 znaków i po przerwie w pisaniu. Ramka = Polska.
 */
export const SUGGESTIONS = {
  url: 'https://photon.komoot.io/api/',
  minLength: 3,
  debounceMs: 400,
  limit: 5,
  /** min. długość, min. szerokość, maks. długość, maks. szerokość. */
  bbox: '14.07,49.0,24.15,54.84',
  countryCode: 'PL',
} as const;

/**
 * Trasy dla alertów „po drodze” (F8): OpenRouteService, darmowy klucz (`environment.openRouteServiceKey`, limit
 * ~2000 tras dziennie – wystarczy, bo trasę liczymy raz, przy zapisie alertu). Przy wzroście – Wariant 2 (backend, Krok 14).
 */
export const ROUTING = {
  url: 'https://api.openrouteservice.org/v2/directions/driving-car/geojson',
  /** Uproszczenie polilinii: przy pasie ≥ 0,5 km 25 m odchyłki nie zmienia wyniku (baza i tak robi `Reduce(25)`). */
  toleranceMeters: 25,
  /** Backend przyjmuje 2–1000 punktów; zostawiamy zapas. */
  maxPoints: 500,
} as const;
