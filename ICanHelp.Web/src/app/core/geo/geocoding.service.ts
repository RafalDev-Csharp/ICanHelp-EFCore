import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { defer, map, switchMap, timer } from 'rxjs';

import { GEOCODING, SUGGESTIONS } from '../config/map';

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

/** Fragment odpowiedzi Photon (GeoJSON), z którego korzystamy. */
interface PhotonResponse {
  features: {
    geometry: { coordinates: [number, number] };
    properties: {
      name?: string;
      street?: string;
      housenumber?: string;
      postcode?: string;
      city?: string;
      district?: string;
      county?: string;
      state?: string;
      countrycode?: string;
    };
  }[];
}

/** „Rynek 1, 16-300 Augustów, województwo podlaskie” – z części, które Photon podał (bez powtórzeń). */
export function photonLabel(properties: PhotonResponse['features'][number]['properties']): string {
  const street = properties.street ? [properties.street, properties.housenumber].filter(Boolean).join(' ') : '';
  const place = [properties.postcode, properties.city ?? properties.district ?? properties.county]
    .filter(Boolean)
    .join(' ');
  const parts = [properties.name, street, place, properties.state].filter((part): part is string => !!part);
  return parts.filter((part, index) => parts.indexOf(part) === index).join(', ');
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

  /** Podpowiedzi w trakcie pisania (Photon, tylko Polska). Opóźnienie i anulowanie poprzednich – po stronie pola. */
  suggest(query: string): Observable<GeocodingResult[]> {
    const params = new HttpParams()
      .set('q', query.trim())
      .set('limit', SUGGESTIONS.limit)
      // Nazwy lokalne (po polsku) zamiast języka przeglądarki.
      .set('lang', 'default')
      .set('bbox', SUGGESTIONS.bbox);
    return this.http.get<PhotonResponse>(SUGGESTIONS.url, { params }).pipe(
      map((response) =>
        (response.features ?? [])
          .filter((feature) => feature.properties.countrycode === SUGGESTIONS.countryCode)
          .map((feature) => ({
            label: photonLabel(feature.properties),
            point: { latitude: feature.geometry.coordinates[1], longitude: feature.geometry.coordinates[0] },
          }))
          .filter(({ label, point }) => !!label && Number.isFinite(point.latitude) && Number.isFinite(point.longitude)),
      ),
    );
  }
}

/** Miejsce ogłoszenia zaokrąglone do ~100 m – na mapie widać okolicę, a dokładny adres pomocnik dostaje po akceptacji. */
export function approximatePoint(point: GeoPoint): GeoPoint {
  return { latitude: roundCoordinate(point.latitude), longitude: roundCoordinate(point.longitude) };
}
