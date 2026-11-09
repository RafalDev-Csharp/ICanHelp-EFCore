import type { Provider } from '@angular/core';

import { environment } from '../../../environments/environment';

import { API_BASE_URL } from './api-client';

/**
 * Adres API dla wygenerowanych klientów (`UsersClient`, `AnnouncementsClient`…). Pusty = ta sama domena
 * (dev: proxy, produkcja: API serwuje frontend).
 */
export function provideApi(): Provider[] {
  return [{ provide: API_BASE_URL, useValue: environment.apiUrl }];
}

/** Żądanie do naszego API (a nie np. do Nominatim czy kafelków map) – tylko tym dokładamy token i tłumaczymy błędy. */
export function isApiUrl(url: string): boolean {
  const path = url.startsWith(environment.apiUrl) ? url.slice(environment.apiUrl.length) : url;
  return path.startsWith('/api/') || path.startsWith('/hubs/');
}

/**
 * Pełny adres zasobu z API podanego jako ścieżka (np. `thumbnailUrl`, `images[].url` – „/api/announcements/…/images/…”).
 * Przy pustym `apiUrl` (ta sama domena) ścieżka zostaje bez zmian.
 */
export function apiResourceUrl(path: string | null | undefined): string | undefined {
  if (!path) {
    return undefined;
  }
  return path.startsWith('/') ? `${environment.apiUrl}${path}` : path;
}
