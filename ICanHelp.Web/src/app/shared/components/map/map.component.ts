import type { ElementRef, OnDestroy } from '@angular/core';
import { ChangeDetectionStrategy, Component, afterNextRender, effect, input, output, viewChild } from '@angular/core';
import * as L from 'leaflet';

import { DEFAULT_MAP_CENTER, DEFAULT_MAP_ZOOM, MAP_TILES } from '../../../core/config/map';
import type { GeoPoint } from '../../../core/geo/geo-point';

/** Pinezka na mapie (np. ogłoszenie). Kliknięcie → zdarzenie `markerClick` z `id`. */
export interface MapMarker extends GeoPoint {
  id: string;
  /** Podpowiedź po najechaniu / dotknięciu. */
  title?: string;
}

/** Okrąg promienia wyszukiwania („blisko mnie” w promieniu N km). */
export interface MapCircle extends GeoPoint {
  radiusKm: number;
}

/** Trasa (alert „po drodze”): linia i pas `radiusKm` po obu stronach – tam szukamy ogłoszeń. */
export interface MapRoute {
  points: GeoPoint[];
  radiusKm: number;
}

/** Metrów na piksel przy danym zoomie (kafelki 256 px, Web Mercator) – do grubości pasu trasy w metrach. */
export function metersPerPixel(latitude: number, zoom: number): number {
  return (156_543.033_92 * Math.cos((latitude * Math.PI) / 180)) / 2 ** zoom;
}

/**
 * Mapa Leaflet + kafelki OpenStreetMap. Widok: `center` + `zoom`, a bez nich – dopasowanie do okręgu albo pinezek
 * (bez niczego – domyślny środek). Wysokość nadaje rodzic (`app-map { height: … }`).
 * Kolory pinezek – w `global.scss` (`.map-pin`, `.map-user`, `.map-radius`), bo Leaflet rysuje poza widokiem komponentu.
 */
@Component({
  selector: 'app-map',
  template: '<div #container class="map"></div>',
  styles: ':host { display: block; } .map { width: 100%; height: 100%; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MapComponent implements OnDestroy {
  readonly markers = input<MapMarker[]>([]);
  readonly center = input<GeoPoint>();
  readonly zoom = input(DEFAULT_MAP_ZOOM);
  readonly circle = input<MapCircle>();
  readonly route = input<MapRoute>();
  /** Pozycja użytkownika – tylko dla niego samego (nigdy nie pokazujemy jej innym). */
  readonly userPosition = input<GeoPoint>();
  /** `false` – mapa poglądowa (np. w szczegółach): bez przesuwania i zoomu, nie przechwytuje przewijania strony. */
  readonly interactive = input(true);
  readonly markerClick = output<string>();
  /** Dotknięcie mapy poza pinezką (tylko mapa interaktywna) – np. wskazanie miejsca ogłoszenia. */
  readonly mapClick = output<GeoPoint>();

  private readonly container = viewChild.required<ElementRef<HTMLDivElement>>('container');
  private map: L.Map | null = null;
  private layers: L.LayerGroup | null = null;
  /** Pas trasy – Leaflet podaje grubość linii w pikselach, więc przeliczamy ją przy każdej zmianie zoomu. */
  private corridor: { line: L.Polyline; radiusKm: number; latitude: number } | null = null;
  private resizeObserver: ResizeObserver | null = null;
  /** Widok dopasowano, gdy mapa miała już rozmiar (strony Ionic na starcie bywają ukryte – wtedy dopasowujemy jeszcze raz). */
  private fittedWithSize = false;

  constructor() {
    afterNextRender(() => this.createMap());
    effect(() => this.render(this.markers(), this.circle(), this.userPosition(), this.route()));
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.map?.remove();
    this.map = null;
  }

  private createMap(): void {
    const element = this.container().nativeElement;
    const interactive = this.interactive();
    this.map = L.map(element, {
      zoomControl: interactive,
      dragging: interactive,
      touchZoom: interactive,
      doubleClickZoom: interactive,
      boxZoom: interactive,
      keyboard: interactive,
      scrollWheelZoom: false,
    });
    L.tileLayer(MAP_TILES.url, { attribution: MAP_TILES.attribution, maxZoom: MAP_TILES.maxZoom }).addTo(this.map);
    this.layers = L.layerGroup().addTo(this.map);
    if (interactive) {
      this.map.on('click', (event: L.LeafletMouseEvent) =>
        this.mapClick.emit({ latitude: event.latlng.lat, longitude: event.latlng.lng }),
      );
    }
    this.map.on('zoomend', () => this.resizeCorridor());
    this.render(this.markers(), this.circle(), this.userPosition(), this.route());

    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => {
        this.map?.invalidateSize();
        if (!this.fittedWithSize) {
          this.fitView();
        }
      });
      this.resizeObserver.observe(element);
    }
  }

  private render(
    markers: MapMarker[],
    circle: MapCircle | undefined,
    user: GeoPoint | undefined,
    route: MapRoute | undefined,
  ): void {
    const layers = this.layers;
    if (!layers) {
      return;
    }
    layers.clearLayers();
    this.corridor = null;

    if (route && route.points.length > 1) {
      const latLngs = route.points.map((point) => L.latLng(point.latitude, point.longitude));
      const line = L.polyline(latLngs, {
        className: 'map-corridor',
        lineCap: 'round',
        lineJoin: 'round',
        interactive: false,
      }).addTo(layers);
      L.polyline(latLngs, { className: 'map-route', weight: 3, interactive: false }).addTo(layers);
      const latitude = latLngs.reduce((sum, point) => sum + point.lat, 0) / latLngs.length;
      this.corridor = { line, radiusKm: route.radiusKm, latitude };
      this.resizeCorridor();
    }

    if (circle) {
      L.circle([circle.latitude, circle.longitude], {
        radius: circle.radiusKm * 1000,
        className: 'map-radius',
        interactive: false,
      }).addTo(layers);
    }
    if (user) {
      L.circleMarker([user.latitude, user.longitude], {
        radius: 7,
        weight: 3,
        className: 'map-user',
        interactive: false,
      })
        .bindTooltip('Twoja okolica')
        .addTo(layers);
    }
    for (const marker of markers) {
      const pin = L.circleMarker([marker.latitude, marker.longitude], {
        radius: 10,
        weight: 2,
        fillOpacity: 1,
        className: 'map-pin',
        interactive: this.interactive(),
        // Kliknięcie pinezki to nie kliknięcie mapy (`mapClick`).
        bubblingMouseEvents: false,
      });
      if (marker.title) {
        pin.bindTooltip(marker.title, { direction: 'top', offset: [0, -8] });
      }
      pin.on('click', () => this.markerClick.emit(marker.id));
      pin.addTo(layers);
    }
    this.fitView();
  }

  /** Pas o szerokości 2 × promień (w metrach) → piksele przy obecnym zoomie (zaokrąglone końce ≈ bufor trasy). */
  private resizeCorridor(): void {
    const corridor = this.corridor;
    if (!corridor || !this.map) {
      return;
    }
    const pixels = (corridor.radiusKm * 2000) / metersPerPixel(corridor.latitude, this.map.getZoom());
    corridor.line.setStyle({ weight: Math.max(4, pixels) });
  }

  private fitView(): void {
    const map = this.map;
    if (!map) {
      return;
    }
    const element = this.container().nativeElement;
    this.fittedWithSize = element.clientWidth > 0 && element.clientHeight > 0;

    const center = this.center();
    const circle = this.circle();
    const markers = this.markers();
    const route = this.route();
    if (center) {
      map.setView([center.latitude, center.longitude], this.zoom());
    } else if (circle) {
      map.fitBounds(L.latLng(circle.latitude, circle.longitude).toBounds(circle.radiusKm * 2000), {
        padding: [16, 16],
      });
    } else if (route && route.points.length > 1) {
      // Cała trasa razem z pasem (granice poszerzone o promień w każdą stronę).
      const bounds = L.latLngBounds(route.points.map((point) => L.latLng(point.latitude, point.longitude)));
      const size = route.radiusKm * 2000;
      const padded = L.latLngBounds(
        bounds.getSouthWest().toBounds(size).getSouthWest(),
        bounds.getNorthEast().toBounds(size).getNorthEast(),
      );
      map.fitBounds(padded, { padding: [16, 16] });
    } else if (markers.length > 0) {
      const bounds = L.latLngBounds(markers.map((marker) => L.latLng(marker.latitude, marker.longitude)));
      map.fitBounds(bounds, { padding: [32, 32], maxZoom: 15 });
    } else {
      map.setView([DEFAULT_MAP_CENTER.latitude, DEFAULT_MAP_CENTER.longitude], DEFAULT_MAP_ZOOM);
    }
  }
}
