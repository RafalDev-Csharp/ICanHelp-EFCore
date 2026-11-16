import { HttpClient, HttpHeaders } from '@angular/common/http';
import { InjectionToken, Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { map, of } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ROUTING } from '../config/map';

import type { GeoPoint } from './geo-point';

/** Klucz OpenRouteService (publiczny – i tak trafia do przeglądarki); w specs podmieniany. */
export const OPEN_ROUTE_SERVICE_KEY = new InjectionToken<string>('OPEN_ROUTE_SERVICE_KEY', {
  providedIn: 'root',
  factory: () => environment.openRouteServiceKey,
});

/** Fragment odpowiedzi OpenRouteService (`/v2/directions/{profile}/geojson`), z którego korzystamy. */
interface OrsDirections {
  features?: { geometry?: { coordinates?: [number, number][] } }[];
}

/** Wynik wyznaczenia trasy: punkty gotowe do zapisu w alercie (2–`ROUTING.maxPoints`). */
export interface PlannedRoute {
  points: GeoPoint[];
  /** `false` – brak klucza ORS, trasa to linia prosta start → cel (do pracy bez klucza). */
  followsRoads: boolean;
}

/**
 * Trasa dla alertu „po drodze” (Krok 14, Wariant 1): frontend pyta OpenRouteService, backend dostaje gotową polilinię.
 * Żądanie idzie poza nasze interceptory (to nie nasze API) – ekran sam mówi o błędzie. Pozycja wychodzi do ORS tylko
 * po kliknięciu „Wyznacz trasę”. Bez klucza (`environment.openRouteServiceKey` puste) – linia prosta.
 */
@Injectable({ providedIn: 'root' })
export class RoutingService {
  private readonly http = inject(HttpClient);
  private readonly key = inject(OPEN_ROUTE_SERVICE_KEY).trim();

  /** Jest klucz ORS – trasa po drogach (bez niego linia prosta). */
  readonly configured = this.key.length > 0;

  plan(start: GeoPoint, end: GeoPoint): Observable<PlannedRoute> {
    if (!this.configured) {
      return of({ points: [start, end], followsRoads: false });
    }
    const headers = new HttpHeaders({ Authorization: this.key });
    const body = {
      coordinates: [
        [start.longitude, start.latitude],
        [end.longitude, end.latitude],
      ],
    };
    return this.http.post<OrsDirections>(ROUTING.url, body, { headers }).pipe(
      map((response) => {
        const coordinates = response.features?.[0]?.geometry?.coordinates ?? [];
        const points = coordinates.map(([longitude, latitude]) => ({ latitude, longitude }));
        return { points: prepareRoute(points.length > 1 ? points : [start, end]), followsRoads: points.length > 1 };
      }),
    );
  }
}

/**
 * Polilinia do zapisu: współrzędne zaokrąglone do ~1 m, bez powtórzonych punktów z rzędu (baza ich nie przyjmie),
 * uproszczona (Douglas–Peucker) – do pasu o szerokości ≥ 0,5 km nie trzeba każdego zakrętu, a backend przyjmuje max 1000.
 */
export function prepareRoute(
  points: GeoPoint[],
  toleranceMeters: number = ROUTING.toleranceMeters,
  maxPoints: number = ROUTING.maxPoints,
): GeoPoint[] {
  const rounded = points
    .map((point) => ({ latitude: round5(point.latitude), longitude: round5(point.longitude) }))
    .filter((point, index, all) => index === 0 || !samePoint(point, all[index - 1]));

  let tolerance = toleranceMeters;
  let simplified = simplify(rounded, tolerance);
  while (simplified.length > maxPoints) {
    tolerance *= 2;
    simplified = simplify(rounded, tolerance);
  }
  return simplified;
}

function round5(value: number): number {
  return Math.round(value * 100_000) / 100_000;
}

function samePoint(a: GeoPoint, b: GeoPoint): boolean {
  return a.latitude === b.latitude && a.longitude === b.longitude;
}

/** Douglas–Peucker na płaskim rzucie (na odcinkach kilkudziesięciu km błąd pomijalny). */
function simplify(points: GeoPoint[], toleranceMeters: number): GeoPoint[] {
  if (points.length < 3) {
    return points;
  }
  const latitude = points[0].latitude;
  const kx = 111_320 * Math.cos((latitude * Math.PI) / 180);
  const ky = 110_540;
  const xy = points.map((point) => ({ x: point.longitude * kx, y: point.latitude * ky }));

  const keep = new Array<boolean>(points.length).fill(false);
  keep[0] = true;
  keep[points.length - 1] = true;
  const stack: [number, number][] = [[0, points.length - 1]];
  while (stack.length > 0) {
    const [first, last] = stack.pop() as [number, number];
    let farthest = -1;
    let maxDistance = 0;
    for (let i = first + 1; i < last; i++) {
      const distance = distanceToSegment(xy[i], xy[first], xy[last]);
      if (distance > maxDistance) {
        maxDistance = distance;
        farthest = i;
      }
    }
    if (farthest > 0 && maxDistance > toleranceMeters) {
      keep[farthest] = true;
      stack.push([first, farthest], [farthest, last]);
    }
  }
  return points.filter((_, index) => keep[index]);
}

function distanceToSegment(
  p: { x: number; y: number },
  a: { x: number; y: number },
  b: { x: number; y: number },
): number {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const lengthSquared = dx * dx + dy * dy;
  const t = lengthSquared === 0 ? 0 : Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / lengthSquared));
  return Math.hypot(p.x - (a.x + t * dx), p.y - (a.y + t * dy));
}
