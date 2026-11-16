import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { IonButton, IonIcon, IonInput, IonItem, IonLabel, IonList, IonSpinner } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { locateOutline, locationOutline, searchOutline } from 'ionicons/icons';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';

import { SUGGESTIONS } from '../../../core/config/map';
import type { GeoPoint } from '../../../core/geo/geo-point';
import type { GeocodingResult } from '../../../core/geo/geocoding.service';
import { GeocodingService } from '../../../core/geo/geocoding.service';
import { GeolocationError, GeolocationService } from '../../../core/geo/geolocation.service';
import { ToastService } from '../../../core/ui/toast.service';

/**
 * Wybór punktu bez mapy: „Moja pozycja” (GPS, tylko po kliknięciu) albo adres – podpowiedzi w trakcie pisania (Photon,
 * od 3 znaków, po przerwie) i pełne wyszukanie po zatwierdzeniu (Nominatim, Enter / lupa).
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
  private readonly typed = new Subject<string>();

  constructor() {
    addIcons({ locateOutline, locationOutline, searchOutline });
    this.typed
      .pipe(
        debounceTime(SUGGESTIONS.debounceMs),
        distinctUntilChanged(),
        // Nowy tekst anuluje poprzednie zapytanie; błąd podpowiedzi = po prostu brak podpowiedzi (zostaje lupa).
        switchMap((query) =>
          query.length < SUGGESTIONS.minLength
            ? of(null)
            : this.geocoding.suggest(query).pipe(catchError(() => of(null))),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((suggestions) => {
        // Pełne wyszukiwanie w toku ma pierwszeństwo. Brak podpowiedzi = lista schowana („Nic nie znaleziono” mówi
        // dopiero pełne wyszukiwanie).
        if (!this.searching()) {
          this.results.set(suggestions && suggestions.length > 0 ? suggestions : null);
        }
      });
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
    this.typed.next(this.query().trim());
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

  /** Wybór z listy: punkt idzie do rodzica, a w polu zostaje nazwa miejsca (bez nowych podpowiedzi dla niej). */
  pick(result: GeocodingResult): void {
    this.query.set(result.label);
    this.results.set(null);
    this.picked.emit(result.point);
  }
}
