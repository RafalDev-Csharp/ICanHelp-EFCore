import { TestBed } from '@angular/core/testing';
import type { Observable } from 'rxjs';
import { of, throwError } from 'rxjs';

import type { GeoPoint } from '../../../core/geo/geo-point';
import type { GeocodingResult } from '../../../core/geo/geocoding.service';
import { GeocodingService } from '../../../core/geo/geocoding.service';
import { GeolocationError, GeolocationService } from '../../../core/geo/geolocation.service';
import { ToastService } from '../../../core/ui/toast.service';

import { PlaceSearchComponent } from './place-search.component';

describe('PlaceSearchComponent', () => {
  const geocoding = { search: vi.fn(), suggest: vi.fn((): Observable<GeocodingResult[]> => of([])) };
  const geolocation = { getCurrentPosition: vi.fn() };
  const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };

  function create(): { search: PlaceSearchComponent; picked: GeoPoint[]; element: HTMLElement; render: () => void } {
    TestBed.configureTestingModule({
      imports: [PlaceSearchComponent],
      providers: [
        { provide: GeocodingService, useValue: geocoding },
        { provide: GeolocationService, useValue: geolocation },
        { provide: ToastService, useValue: toast },
      ],
    });
    const fixture = TestBed.createComponent(PlaceSearchComponent);
    const picked: GeoPoint[] = [];
    fixture.componentInstance.picked.subscribe((point) => picked.push(point));
    fixture.detectChanges();
    return {
      search: fixture.componentInstance,
      picked,
      element: fixture.nativeElement as HTMLElement,
      render: () => fixture.detectChanges(),
    };
  }

  beforeEach(() => vi.clearAllMocks());

  it('searches only after at least 3 characters and emits the chosen result', () => {
    const point = { latitude: 53.844, longitude: 22.979 };
    geocoding.search.mockReturnValue(of([{ label: 'Rynek, Augustów', point }]));
    const { search, picked, element, render } = create();

    search.setQuery('Ry');
    search.search();
    expect(geocoding.search).not.toHaveBeenCalled();

    search.setQuery('Rynek Augustów');
    search.search();
    render();
    expect(element.textContent).toContain('Rynek, Augustów');

    (element.querySelector('ion-item[button]') as HTMLElement).click();
    expect(picked).toEqual([point]);
  });

  it('emits my position or explains why it is missing', async () => {
    const point = { latitude: 53.84, longitude: 22.98 };
    geolocation.getCurrentPosition.mockResolvedValueOnce(point);
    const { search, picked } = create();

    await search.useMyPosition();
    expect(picked).toEqual([point]);

    geolocation.getCurrentPosition.mockRejectedValueOnce(new GeolocationError('Brak zgody na lokalizację'));
    await search.useMyPosition();
    expect(toast.error).toHaveBeenCalledWith('Brak zgody na lokalizację');
  });

  it('suggests places while typing (from 3 characters, after a pause) and fills the field with the picked one', () => {
    vi.useFakeTimers();
    try {
      const point = { latitude: 52.23, longitude: 21.01 };
      geocoding.suggest.mockReturnValue(of([{ label: 'Marszałkowska, Warszawa', point }]));
      const { search, picked, element, render } = create();

      search.setQuery('Wa');
      vi.advanceTimersByTime(500);
      expect(geocoding.suggest).not.toHaveBeenCalled();

      search.setQuery('Warsz');
      search.setQuery('Warszawa Marsz');
      vi.advanceTimersByTime(500);
      render();
      expect(geocoding.suggest).toHaveBeenCalledTimes(1);
      expect(geocoding.suggest).toHaveBeenCalledWith('Warszawa Marsz');
      expect(element.textContent).toContain('Marszałkowska, Warszawa');

      (element.querySelector('ion-item[button]') as HTMLElement).click();
      expect(picked).toEqual([point]);
      expect(geocoding.search).not.toHaveBeenCalled();
    } finally {
      vi.useRealTimers();
    }
  });

  it('shows an error when the address search fails', () => {
    geocoding.search.mockReturnValue(throwError(() => new Error('offline')));
    const { search } = create();

    search.setQuery('Rynek Augustów');
    search.search();

    expect(toast.error).toHaveBeenCalled();
  });
});
