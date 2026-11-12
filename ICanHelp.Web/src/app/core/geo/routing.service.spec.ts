import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { ROUTING } from '../config/map';

import type { GeoPoint } from './geo-point';
import { OPEN_ROUTE_SERVICE_KEY, RoutingService, prepareRoute } from './routing.service';

describe('RoutingService', () => {
  const start: GeoPoint = { latitude: 54.1, longitude: 22.93 };
  const end: GeoPoint = { latitude: 53.844, longitude: 22.979 };

  function setup(key: string): { service: RoutingService; http: HttpTestingController } {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: OPEN_ROUTE_SERVICE_KEY, useValue: key }],
    });
    return { service: TestBed.inject(RoutingService), http: TestBed.inject(HttpTestingController) };
  }

  it('asks OpenRouteService with the key and returns the road as points', async () => {
    const { service, http } = setup('ors-key');
    expect(service.configured).toBe(true);

    const result = firstValueFrom(service.plan(start, end));
    const request = http.expectOne(ROUTING.url);
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Authorization')).toBe('ors-key');
    expect(request.request.body).toEqual({
      coordinates: [
        [22.93, 54.1],
        [22.979, 53.844],
      ],
    });
    request.flush({
      features: [
        {
          geometry: {
            coordinates: [
              [22.93, 54.1],
              [22.95, 53.97],
              [22.95, 53.97],
              [22.979, 53.844],
            ],
          },
        },
      ],
    });

    expect(await result).toEqual({
      points: [start, { latitude: 53.97, longitude: 22.95 }, end],
      followsRoads: true,
    });
    http.verify();
  });

  it('falls back to a straight line without a key (no request)', async () => {
    const { service, http } = setup('  ');

    expect(service.configured).toBe(false);
    expect(await firstValueFrom(service.plan(start, end))).toEqual({ points: [start, end], followsRoads: false });
    http.verify();
  });
});

describe('prepareRoute', () => {
  it('rounds to ~1 m and drops repeated points', () => {
    expect(
      prepareRoute([
        { latitude: 54.000001, longitude: 22.000001 },
        { latitude: 54.000002, longitude: 22.000002 },
        { latitude: 54.1, longitude: 22.1 },
      ]),
    ).toEqual([
      { latitude: 54, longitude: 22 },
      { latitude: 54.1, longitude: 22.1 },
    ]);
  });

  it('removes points that barely change the line', () => {
    const straight = Array.from({ length: 50 }, (_, i) => ({ latitude: 54 + i * 0.001, longitude: 22 }));
    const withBend = [...straight, { latitude: 54.049, longitude: 22.05 }];

    expect(prepareRoute(straight)).toHaveLength(2);
    expect(prepareRoute(withBend)).toHaveLength(3);
  });

  it('keeps the route within the point limit', () => {
    const zigzag = Array.from({ length: 2000 }, (_, i) => ({
      latitude: 54 + i * 0.001,
      longitude: 22 + (i % 2) * 0.01,
    }));

    const points = prepareRoute(zigzag, 25, 100);

    expect(points.length).toBeLessThanOrEqual(100);
    expect(points[0]).toEqual(zigzag[0]);
    expect(points[points.length - 1]).toEqual(zigzag[zigzag.length - 1]);
  });
});
