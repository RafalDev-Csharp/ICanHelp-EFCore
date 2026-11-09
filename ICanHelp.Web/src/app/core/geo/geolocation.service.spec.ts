import { TestBed } from '@angular/core/testing';

import { GeolocationError, GeolocationService, roundCoordinate } from './geolocation.service';

describe('GeolocationService', () => {
  const original = Object.getOwnPropertyDescriptor(navigator, 'geolocation');

  function fakeGeolocation(getCurrentPosition: Geolocation['getCurrentPosition'] | undefined): void {
    Object.defineProperty(navigator, 'geolocation', {
      configurable: true,
      value: getCurrentPosition ? { getCurrentPosition } : undefined,
    });
  }

  afterEach(() => {
    if (original) {
      Object.defineProperty(navigator, 'geolocation', original);
    } else {
      delete (navigator as unknown as Record<string, unknown>)['geolocation'];
    }
  });

  it('returns the position rounded to about 100 m', async () => {
    fakeGeolocation((success) =>
      success({ coords: { latitude: 53.843712, longitude: 22.978549 } } as GeolocationPosition),
    );

    await expect(TestBed.inject(GeolocationService).getCurrentPosition()).resolves.toEqual({
      latitude: 53.844,
      longitude: 22.979,
    });
  });

  it('explains a denied permission in Polish', async () => {
    fakeGeolocation((_success, error) => error?.({ code: 1 } as GeolocationPositionError));

    const result = TestBed.inject(GeolocationService).getCurrentPosition();

    await expect(result).rejects.toBeInstanceOf(GeolocationError);
    await expect(result).rejects.toThrow(/zgody na lokalizację/);
  });

  it('fails when the browser has no geolocation', async () => {
    fakeGeolocation(undefined);

    await expect(TestBed.inject(GeolocationService).getCurrentPosition()).rejects.toThrow(/nie udostępnia/);
  });

  it('rounds coordinates to three decimals', () => {
    expect(roundCoordinate(22.97849)).toBe(22.978);
  });
});
