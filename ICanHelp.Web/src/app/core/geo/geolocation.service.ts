import { Injectable } from '@angular/core';

import type { GeoPoint } from './geo-point';

/** Błąd ustalania pozycji z komunikatem po polsku (do pokazania w toaście). */
export class GeolocationError extends Error {}

const MESSAGES: Readonly<Record<number, string>> = {
  1: 'Brak zgody na lokalizację – zezwól na nią w ustawieniach przeglądarki.',
  2: 'Nie udało się ustalić Twojej pozycji – spróbuj ponownie.',
  3: 'Ustalanie pozycji trwa za długo – spróbuj ponownie.',
};

/** Zaokrąglenie do ~100 m: do szukania „w okolicy” wystarczy, a dokładna pozycja nie wychodzi z telefonu. */
const PRECISION = 1_000;

/**
 * Pozycja użytkownika – wyłącznie na jego wyraźną akcję (np. „Blisko mnie”), nigdy w tle.
 * Wymaga HTTPS (wyjątek: localhost) – na zwykłym http przeglądarka od razu odmawia (kod 1).
 */
@Injectable({ providedIn: 'root' })
export class GeolocationService {
  getCurrentPosition(): Promise<GeoPoint> {
    const geolocation = typeof navigator === 'undefined' ? undefined : navigator.geolocation;
    if (!geolocation) {
      return Promise.reject(new GeolocationError('Ta przeglądarka nie udostępnia lokalizacji.'));
    }
    return new Promise((resolve, reject) =>
      geolocation.getCurrentPosition(
        (position) =>
          resolve({
            latitude: roundCoordinate(position.coords.latitude),
            longitude: roundCoordinate(position.coords.longitude),
          }),
        (error) => reject(new GeolocationError(MESSAGES[error.code] ?? MESSAGES[2])),
        { enableHighAccuracy: false, timeout: 15_000, maximumAge: 5 * 60_000 },
      ),
    );
  }
}

export function roundCoordinate(value: number): number {
  return Math.round(value * PRECISION) / PRECISION;
}
