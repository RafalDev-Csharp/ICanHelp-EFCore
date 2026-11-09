import type { AnnouncementStatus } from '../../../core/api/api-client';
import type { GeoPoint } from '../../../core/geo/geo-point';

export const PAGE_SIZE = 20;
export const RADIUS_OPTIONS_KM: readonly number[] = [2, 5, 10, 25, 50];
export const DEFAULT_RADIUS_KM = 10;

/** Filtry listy ogłoszeń. Wszystkie opcjonalne poza promieniem (używanym tylko razem z `near`). */
export interface AnnouncementFilters {
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
  page: number;
  pageSize: number;
}

/**
 * Filtry → zapytanie. Przeglądamy tylko ogłoszenia czekające na oferty (`Open`) – pozostałe statusy pokaże
 * „Moje ogłoszenia” (F4). Bez `near` nie wysyłamy promienia (API odrzuciłoby go bez współrzędnych).
 */
export function toAnnouncementQuery(filters: AnnouncementFilters, page: number): AnnouncementQuery {
  const query: AnnouncementQuery = { status: 'Open', page, pageSize: PAGE_SIZE };
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
  return [filters.categoryId, filters.subcategoryId, filters.near].filter((value) => value !== undefined).length;
}
