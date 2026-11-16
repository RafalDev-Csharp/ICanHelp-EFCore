import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { IonNote } from '@ionic/angular';

import type { GeoPoint } from '../../../core/geo/geo-point';
import { approximatePoint } from '../../../core/geo/geocoding.service';
import type { MapCircle, MapMarker } from '../../../shared/components/map/map.component';
import { MapComponent } from '../../../shared/components/map/map.component';
import { PlaceSearchComponent } from '../../../shared/components/place-search/place-search.component';

/**
 * Jeden punkt: „Moja pozycja” (GPS, po kliknięciu), wyszukanie adresu (Nominatim) albo dotknięcie mapy.
 * Każdy punkt zaokrąglamy do ~100 m – inni widzą okolicę, nie dokładny adres.
 * Domyślnie miejsce ogłoszenia; alert obszaru podaje `radiusKm` (okrąg na mapie), własny podpis i podpowiedź.
 */
@Component({
  selector: 'app-location-picker',
  templateUrl: './location-picker.component.html',
  styleUrls: ['./location-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IonNote, MapComponent, PlaceSearchComponent],
})
export class LocationPickerComponent {
  readonly point = model<GeoPoint | null>(null);
  readonly markerTitle = input('Miejsce ogłoszenia');
  /** Promień wokół punktu (alert „w okolicy”) – mapa pokazuje okrąg i dopasowuje do niego widok. */
  readonly radiusKm = input<number>();
  /** Podpowiedź pod mapą po wybraniu punktu (domyślnie – o prywatności miejsca ogłoszenia). */
  readonly hint = input<string>();

  protected readonly markers = computed<MapMarker[]>(() => {
    const point = this.point();
    return point ? [{ id: 'point', ...point, title: this.markerTitle() }] : [];
  });

  protected readonly circle = computed<MapCircle | undefined>(() => {
    const point = this.point();
    const radiusKm = this.radiusKm();
    return point && radiusKm ? { ...point, radiusKm } : undefined;
  });

  /** Z okręgiem widok dopasowuje mapa (cały okrąg), bez niego – zbliżenie na punkt. */
  protected readonly center = computed(() => (this.circle() ? undefined : (this.point() ?? undefined)));

  choose(point: GeoPoint): void {
    this.point.set(approximatePoint(point));
  }
}
