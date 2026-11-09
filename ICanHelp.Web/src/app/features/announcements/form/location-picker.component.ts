import { ChangeDetectionStrategy, Component, computed, inject, model, signal } from '@angular/core';
import { IonButton, IonIcon, IonInput, IonItem, IonLabel, IonList, IonNote, IonSpinner } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { locateOutline, locationOutline, searchOutline } from 'ionicons/icons';

import type { GeoPoint } from '../../../core/geo/geo-point';
import type { GeocodingResult } from '../../../core/geo/geocoding.service';
import { GeocodingService, approximatePoint } from '../../../core/geo/geocoding.service';
import { GeolocationError, GeolocationService } from '../../../core/geo/geolocation.service';
import { ToastService } from '../../../core/ui/toast.service';
import type { MapMarker } from '../../../shared/components/map/map.component';
import { MapComponent } from '../../../shared/components/map/map.component';

/**
 * Miejsce ogłoszenia: „Moja pozycja” (GPS, po kliknięciu), wyszukanie adresu (Nominatim) albo dotknięcie mapy.
 * Każdy punkt zaokrąglamy do ~100 m – inni widzą okolicę, nie dokładny adres.
 */
@Component({
  selector: 'app-location-picker',
  templateUrl: './location-picker.component.html',
  styleUrls: ['./location-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IonButton, IonIcon, IonInput, IonItem, IonLabel, IonList, IonNote, IonSpinner, MapComponent],
})
export class LocationPickerComponent {
  private readonly geolocation = inject(GeolocationService);
  private readonly geocoding = inject(GeocodingService);
  private readonly toast = inject(ToastService);

  readonly point = model<GeoPoint | null>(null);

  protected readonly query = signal('');
  protected readonly results = signal<GeocodingResult[] | null>(null);
  protected readonly searching = signal(false);
  protected readonly locating = signal(false);

  protected readonly markers = computed<MapMarker[]>(() => {
    const point = this.point();
    return point ? [{ id: 'announcement', ...point, title: 'Miejsce ogłoszenia' }] : [];
  });

  constructor() {
    addIcons({ locateOutline, locationOutline, searchOutline });
  }

  async useMyPosition(): Promise<void> {
    if (this.locating()) {
      return;
    }
    this.locating.set(true);
    try {
      this.choose(await this.geolocation.getCurrentPosition());
    } catch (error) {
      void this.toast.error(error instanceof GeolocationError ? error.message : 'Nie udało się ustalić pozycji.');
    } finally {
      this.locating.set(false);
    }
  }

  setQuery(value: unknown): void {
    this.query.set(typeof value === 'string' ? value : '');
  }

  search(): void {
    const query = this.query().trim();
    if (query.length < 3 || this.searching()) {
      return;
    }
    this.searching.set(true);
    this.geocoding.search(query).subscribe({
      next: (results) => {
        this.results.set(results);
        this.searching.set(false);
      },
      error: () => {
        this.searching.set(false);
        void this.toast.error('Nie udało się wyszukać adresu – spróbuj ponownie albo wskaż miejsce na mapie.');
      },
    });
  }

  pick(result: GeocodingResult): void {
    this.choose(result.point);
    this.results.set(null);
  }

  choose(point: GeoPoint): void {
    this.point.set(approximatePoint(point));
  }
}
