import type { OnInit } from '@angular/core';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonInfiniteScroll,
  IonInfiniteScrollContent,
  IonLabel,
  IonList,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonSelect,
  IonSelectOption,
  IonSkeletonText,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { close, cloudOfflineOutline, locateOutline, searchOutline } from 'ionicons/icons';

import { CategoriesService } from '../../core/categories/categories.service';
import { GeolocationError, GeolocationService } from '../../core/geo/geolocation.service';
import { ToastService } from '../../core/ui/toast.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import type { MapCircle, MapMarker } from '../../shared/components/map/map.component';
import { MapComponent } from '../../shared/components/map/map.component';
import { formatPrice } from '../../shared/pipes/price.pipe';
import { pluralPl } from '../../shared/text/plural';

import { LikesService } from './likes/likes.service';
import { AnnouncementCardComponent } from './list/announcement-card.component';
import { RADIUS_OPTIONS_KM, activeFilterCount } from './list/announcement-filters';
import { AnnouncementListStore } from './list/announcement-list.store';

export type AnnouncementsView = 'list' | 'map';

/** Ionic: `ion-refresher` i `ion-infinite-scroll` trzeba „zamknąć” po wczytaniu danych. */
interface CompletableTarget {
  complete(): Promise<void>;
}

/**
 * Przeglądanie ogłoszeń (bez logowania): lista z przewijaniem bez końca albo mapa, filtry kategorii i „blisko mnie”.
 * Pokazujemy tylko ogłoszenia czekające na oferty.
 */
@Component({
  selector: 'app-announcements',
  templateUrl: './announcements.page.html',
  styleUrls: ['./announcements.page.scss'],
  providers: [AnnouncementListStore],
  imports: [
    BrandComponent,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonContent,
    IonSegment,
    IonSegmentButton,
    IonLabel,
    IonIcon,
    IonSpinner,
    IonSelect,
    IonSelectOption,
    IonList,
    IonRefresher,
    IonRefresherContent,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    IonSkeletonText,
    IonButton,
    EmptyStateComponent,
    AnnouncementCardComponent,
    MapComponent,
  ],
})
export class AnnouncementsPage implements OnInit, ViewWillEnter {
  private readonly geolocation = inject(GeolocationService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly store = inject(AnnouncementListStore);
  protected readonly categories = inject(CategoriesService);
  protected readonly likes = inject(LikesService);

  protected readonly radiusOptions = RADIUS_OPTIONS_KM;
  protected readonly skeletons = [1, 2, 3, 4, 5];
  protected readonly view = signal<AnnouncementsView>('list');
  protected readonly locating = signal(false);
  /** Pinezka wybrana na mapie – jej ogłoszenie pokazujemy w karcie na dole mapy. */
  protected readonly selectedId = signal<string | null>(null);
  /** Pierwsze wejście wczytuje `ngOnInit` – odświeżamy dopiero przy kolejnych. */
  private entered = false;

  protected readonly filters = this.store.filters;
  protected readonly hasFilters = computed(() => activeFilterCount(this.filters()) > 0);
  protected readonly subcategories = computed(() => this.categories.subcategoriesOf(this.filters().categoryId));
  protected readonly selected = computed(() => this.store.items().find((item) => item.id === this.selectedId()));
  protected readonly markers = computed<MapMarker[]>(() =>
    this.store.items().map((item) => ({
      id: item.id,
      latitude: item.location.latitude,
      longitude: item.location.longitude,
      title: `${item.title} · ${formatPrice(item.budget.amount)}`,
    })),
  );
  protected readonly searchCircle = computed<MapCircle | undefined>(() => {
    const { near, radiusKm } = this.filters();
    return near ? { ...near, radiusKm } : undefined;
  });
  protected readonly mapSummary = computed(() => {
    const count = this.store.items().length;
    return `${count} ${pluralPl(count, 'ogłoszenie', 'ogłoszenia', 'ogłoszeń')} na mapie`;
  });

  constructor() {
    addIcons({ locateOutline, searchOutline, cloudOfflineOutline, close });
  }

  ngOnInit(): void {
    // Błąd pokazał już interceptor; bez kategorii lista działa dalej (filtr kategorii jest wtedy pusty).
    this.categories.load().subscribe({ error: () => undefined });
    void this.store.reload();
  }

  /**
   * Przy każdym wejściu na zakładkę (np. po zalogowaniu): serduszka na kartach – wczytane raz na sesję. Przy powrocie
   * (ze szczegółów, z innej zakładki) – pierwsza strona od nowa: ogłoszenie mogło w międzyczasie przestać czekać na
   * oferty (akceptacja, anulowanie) albo dojść nowe. Dotychczasowe karty zostają do przyjścia wyników.
   */
  ionViewWillEnter(): void {
    this.likes.load().subscribe({ error: () => undefined });
    if (this.entered && !this.store.loading()) {
      void this.store.reload();
    }
    this.entered = true;
  }

  setView(value: unknown): void {
    if (value === 'list' || value === 'map') {
      this.view.set(value);
      this.selectedId.set(null);
    }
  }

  setCategory(value: unknown): void {
    const categoryId = typeof value === 'string' && value !== '' ? value : undefined;
    if (categoryId !== this.filters().categoryId) {
      void this.store.setFilters({ categoryId, subcategoryId: undefined });
    }
  }

  setSubcategory(value: unknown): void {
    const subcategoryId = typeof value === 'string' && value !== '' ? value : undefined;
    if (subcategoryId !== this.filters().subcategoryId) {
      void this.store.setFilters({ subcategoryId });
    }
  }

  setRadius(value: unknown): void {
    const radiusKm = Number(value);
    if (this.radiusOptions.includes(radiusKm) && radiusKm !== this.filters().radiusKm) {
      void this.store.setFilters({ radiusKm });
    }
  }

  /** „Blisko mnie”: pozycję pobieramy dopiero teraz, po kliknięciu. Drugie kliknięcie wyłącza filtr. */
  async toggleNearMe(): Promise<void> {
    if (this.filters().near) {
      await this.store.setFilters({ near: undefined });
      return;
    }
    this.locating.set(true);
    try {
      const near = await this.geolocation.getCurrentPosition();
      void this.store.setFilters({ near });
    } catch (error) {
      await this.toast.error(error instanceof GeolocationError ? error.message : 'Nie udało się ustalić pozycji.');
    } finally {
      this.locating.set(false);
    }
  }

  clearFilters(): void {
    void this.store.setFilters({ categoryId: undefined, subcategoryId: undefined, near: undefined });
  }

  refresh(event: Event): void {
    void this.store.reload().then(() => (event.target as unknown as CompletableTarget).complete());
  }

  loadMore(event: Event): void {
    void this.store.loadMore().then(() => (event.target as unknown as CompletableTarget).complete());
  }

  selectMarker(id: string): void {
    this.selectedId.set(id);
  }

  openAnnouncement(id: string): void {
    void this.router.navigate(['/announcements', id]);
  }
}
