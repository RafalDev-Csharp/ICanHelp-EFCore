import { ChangeDetectionStrategy, Component, computed, inject, input, model, signal } from '@angular/core';
import { IonButton, IonIcon, IonLabel, IonNote, IonSegment, IonSegmentButton, IonSpinner } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { flagOutline, navigateOutline } from 'ionicons/icons';

import type { GeoPoint } from '../../core/geo/geo-point';
import { approximatePoint } from '../../core/geo/geocoding.service';
import { RoutingService } from '../../core/geo/routing.service';
import { ToastService } from '../../core/ui/toast.service';
import type { MapMarker, MapRoute } from '../../shared/components/map/map.component';
import { MapComponent } from '../../shared/components/map/map.component';
import { PlaceSearchComponent } from '../../shared/components/place-search/place-search.component';

export type RouteEnd = 'start' | 'end';

/**
 * Trasa alertu „po drodze”: start i cel (adres, moja pozycja albo dotknięcie mapy – do końca wybranego przełącznikiem),
 * potem droga z OpenRouteService (`RoutingService`). Mapa pokazuje drogę i pas o szerokości promienia.
 * Zmiana startu lub celu unieważnia drogę; nowa liczy się sama, gdy oba punkty są wybrane.
 */
@Component({
  selector: 'app-route-picker',
  templateUrl: './route-picker.component.html',
  styleUrls: ['./route-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    IonButton,
    IonIcon,
    IonLabel,
    IonNote,
    IonSegment,
    IonSegmentButton,
    IonSpinner,
    MapComponent,
    PlaceSearchComponent,
  ],
})
export class RoutePickerComponent {
  private readonly routing = inject(RoutingService);
  private readonly toast = inject(ToastService);

  readonly start = model<GeoPoint | null>(null);
  readonly end = model<GeoPoint | null>(null);
  readonly points = model<GeoPoint[]>([]);
  readonly radiusKm = input(2);

  protected readonly active = signal<RouteEnd>('start');
  protected readonly planning = signal(false);
  protected readonly straightLine = signal(false);
  protected readonly roadsAvailable = this.routing.configured;

  protected readonly markers = computed<MapMarker[]>(() => {
    const markers: MapMarker[] = [];
    const start = this.start();
    const end = this.end();
    if (start) {
      markers.push({ id: 'start', ...start, title: 'Start' });
    }
    if (end) {
      markers.push({ id: 'end', ...end, title: 'Cel' });
    }
    return markers;
  });

  protected readonly route = computed<MapRoute | undefined>(() => {
    const points = this.points();
    return points.length > 1 ? { points, radiusKm: this.radiusKm() } : undefined;
  });

  constructor() {
    addIcons({ flagOutline, navigateOutline });
  }

  setActive(value: unknown): void {
    if (value === 'start' || value === 'end') {
      this.active.set(value);
    }
  }

  /** Punkt dla wybranego końca; po starcie przełączamy na cel. Oba gotowe → trasa. */
  choose(point: GeoPoint): void {
    const rounded = approximatePoint(point);
    if (this.active() === 'start') {
      this.start.set(rounded);
      if (!this.end()) {
        this.active.set('end');
      }
    } else {
      this.end.set(rounded);
    }
    this.points.set([]);
    this.straightLine.set(false);
    this.plan();
  }

  plan(): void {
    const start = this.start();
    const end = this.end();
    if (!start || !end || this.planning()) {
      return;
    }
    if (start.latitude === end.latitude && start.longitude === end.longitude) {
      void this.toast.error('Start i cel to to samo miejsce – wybierz inny cel.');
      return;
    }
    this.planning.set(true);
    this.routing.plan(start, end).subscribe({
      next: (route) => {
        this.points.set(route.points);
        this.straightLine.set(!route.followsRoads);
        this.planning.set(false);
      },
      error: () => {
        this.planning.set(false);
        void this.toast.error('Nie udało się wyznaczyć trasy – spróbuj ponownie za chwilę.');
      },
    });
  }
}
