import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { defer, map, switchMap, timer } from 'rxjs';

import { GEOCODING } from '../config/map';

import type { GeoPoint } from './geo-point';
import { roundCoordinate } from './geolocation.service';

/** Wynik wyszukiwania adresu. */
export interface GeocodingResult {
  /** Pełna nazwa miejsca, np. „Rynek Zygmunta Augusta, Augustów, …”. */
  label: string;
  point: GeoPoint;
}

/** Fragment odpowiedzi Nominatim (`format=jsonv2`), z którego korzystamy. */
interface NominatimPlace {
  display_name: string;
  lat: string;
  lon: string;
}

/**
 * Adres → punkt na mapie (Nominatim). Zapytania idą bez tokenu i poza interceptorem błędów (to nie nasze API),
 * więc ekran sam mówi o błędzie. Kolejne zapytanie czeka, aż minie limit Nominatim (1/s).
 */
@Injectable({ providedIn: 'root' })
export class GeocodingService {
  private readonly http = inject(HttpClient);
  private lastRequestAt = 0;

  search(query: string): Observable<GeocodingResult[]> {
    const params = new HttpParams()
      .set('q', query.trim())
      .set('format', 'jsonv2')
      .set('limit', GEOCODING.limit)
      .set('countrycodes', GEOCODING.countryCodes)
      .set('accept-language', 'pl');

    return defer(() => {
      const wait = Math.max(0, this.lastRequestAt + GEOCODING.minIntervalMs - Date.now());
      this.lastRequestAt = Date.now() + wait;
      return timer(wait);
    }).pipe(
      switchMap(() => this.http.get<NominatimPlace[]>(GEOCODING.url, { params })),
      map((places) =>
        places
          .map((place) => ({
            label: place.display_name,
            point: { latitude: Number(place.lat), longitude: Number(place.lon) },
          }))
          .filter(({ point }) => Number.isFinite(point.latitude) && Number.isFinite(point.longitude)),
      ),
    );
  }
}

/** Miejsce ogłoszenia zaokrąglone do ~100 m – na mapie widać okolicę, a dokładny adres pomocnik dostaje po akceptacji. */
export function approximatePoint(point: GeoPoint): GeoPoint {
  return { latitude: roundCoordinate(point.latitude), longitude: roundCoordinate(point.longitude) };
}
