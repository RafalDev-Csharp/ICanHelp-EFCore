import type { AnnouncementStatus } from '../../../core/api/api-client';
import type { GeoPoint } from '../../../core/geo/geo-point';

export const PAGE_SIZE = 20;
export const RADIUS_OPTIONS_KM: readonly number[] = [2, 5, 10, 25, 50];
export const DEFAULT_RADIUS_KM = 10;
/** Wartość opcji „Bez limitu” w wyborze promienia (F11 F) – wyłącza „Blisko mnie”, nie trafia do API. */
export const NO_RADIUS_LIMIT = 0;
/** Jak `GetAnnouncements.MaxPhraseLength` w API (dłuższa fraza → 400). */
export const MAX_PHRASE_LENGTH = 100;

/** Filtry listy ogłoszeń. Wszystkie opcjonalne poza promieniem (używanym tylko razem z `near`). */
export interface AnnouncementFilters {
  /** Szukany tekst w tytule i opisie (wyszukiwarka zakładki „Szukaj”). */
  phrase?: string;
  categoryId?: string;
  subcategoryId?: string;
  /** Środek szukania „blisko mnie” – ustawiany wyłącznie po kliknięciu użytkownika. */
  near?: GeoPoint;
  radiusKm: number;
}

export const DEFAULT_FILTERS: AnnouncementFilters = { radiusKm: DEFAULT_RADIUS_KM };

/** Parametry `GET api/announcements` (nazwy jak w `AnnouncementsClient.getAll`). */
export interface AnnouncementQuery {
  categoryId?: string;
  subcategoryId?: string;
  status: AnnouncementStatus;
  latitude?: number;
  longitude?: number;
  radiusKm?: number;
  phrase?: string;
  page: number;
  pageSize: number;
}

/**
 * Filtry → zapytanie. Przeglądamy tylko ogłoszenia czekające na oferty (`Open`) – pozostałe statusy pokazuje
 * zakładka „Zlecenia”. Bez `near` nie wysyłamy promienia (API odrzuciłoby go bez współrzędnych).
 */
export function toAnnouncementQuery(filters: AnnouncementFilters, page: number): AnnouncementQuery {
  const query: AnnouncementQuery = { status: 'Open', page, pageSize: PAGE_SIZE };
  const phrase = normalizePhrase(filters.phrase);
  if (phrase) {
    query.phrase = phrase;
  }
  if (filters.categoryId) {
    query.categoryId = filters.categoryId;
    if (filters.subcategoryId) {
      query.subcategoryId = filters.subcategoryId;
    }
  }
  if (filters.near) {
    query.latitude = filters.near.latitude;
    query.longitude = filters.near.longitude;
    query.radiusKm = filters.radiusKm;
  }
  return query;
}

/** Ile filtrów zawęża listę (do licznika / pustego stanu). */
export function activeFilterCount(filters: AnnouncementFilters): number {
  return [normalizePhrase(filters.phrase), filters.categoryId, filters.subcategoryId, filters.near].filter(
    (value) => value !== undefined,
  ).length;
}

/** Fraza z wyszukiwarki: bez spacji na brzegach, maks. {@link MAX_PHRASE_LENGTH} znaków; pusta → `undefined`. */
export function normalizePhrase(value: unknown): string | undefined {
  const phrase = typeof value === 'string' ? value.trim().slice(0, MAX_PHRASE_LENGTH).trim() : '';
  return phrase === '' ? undefined : phrase;
}
