import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { GEOCODING } from '../config/map';

import { GeocodingService, approximatePoint } from './geocoding.service';

describe('GeocodingService', () => {
  let service: GeocodingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(GeocodingService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('searches Nominatim in Poland and maps the places to points', async () => {
    const result = firstValueFrom(service.search('  Rynek, Augustów '));

    const request = await vi.waitFor(() =>
      http.expectOne((req) => req.url === GEOCODING.url && req.params.get('q') === 'Rynek, Augustów'),
    );
    expect(request.request.params.get('countrycodes')).toBe('pl');
    expect(request.request.params.get('format')).toBe('jsonv2');
    request.flush([
      { display_name: 'Rynek Zygmunta Augusta, Augustów', lat: '53.8437', lon: '22.9785' },
      { display_name: 'Zepsuty wynik', lat: 'x', lon: '22' },
    ]);

    expect(await result).toEqual([
      { label: 'Rynek Zygmunta Augusta, Augustów', point: { latitude: 53.8437, longitude: 22.9785 } },
    ]);
  });
});

describe('approximatePoint', () => {
  it('rounds to about 100 m', () => {
    expect(approximatePoint({ latitude: 53.84371234, longitude: 22.97859 })).toEqual({
      latitude: 53.844,
      longitude: 22.979,
    });
  });
});
