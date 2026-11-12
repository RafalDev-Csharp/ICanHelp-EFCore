import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { IonButton, IonIcon, IonInput, IonItem, IonLabel, IonList, IonSpinner } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { locateOutline, locationOutline, searchOutline } from 'ionicons/icons';

import type { GeoPoint } from '../../../core/geo/geo-point';
import type { GeocodingResult } from '../../../core/geo/geocoding.service';
import { GeocodingService } from '../../../core/geo/geocoding.service';
import { GeolocationError, GeolocationService } from '../../../core/geo/geolocation.service';
import { ToastService } from '../../../core/ui/toast.service';

/**
 * Wybór punktu bez mapy: „Moja pozycja” (GPS, tylko po kliknięciu) albo wyszukanie adresu (Nominatim, po zatwierdzeniu).
 * Wybrany punkt → `picked`. Mapę (dotknięcie) dokłada rodzic – miejsce ogłoszenia, środek alertu, start i cel trasy.
 */
@Component({
  selector: 'app-place-search',
  templateUrl: './place-search.component.html',
  styleUrls: ['./place-search.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IonButton, IonIcon, IonInput, IonItem, IonLabel, IonList, IonSpinner],
})
export class PlaceSearchComponent {
  private readonly geolocation = inject(GeolocationService);
  private readonly geocoding = inject(GeocodingService);
  private readonly toast = inject(ToastService);

  readonly label = input('Adres');
  readonly placeholder = input('np. Rynek 1, Augustów');
  readonly picked = output<GeoPoint>();

  protected readonly query = signal('');
  protected readonly results = signal<GeocodingResult[] | null>(null);
  protected readonly searching = signal(false);
  protected readonly locating = signal(false);

  constructor() {
    addIcons({ locateOutline, locationOutline, searchOutline });
  }

  async useMyPosition(): Promise<void> {
    if (this.locating()) {
      return;
    }
    this.locating.set(true);
    try {
      this.picked.emit(await this.geolocation.getCurrentPosition());
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
    this.results.set(null);
    this.picked.emit(result.point);
  }
}
