import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonBadge,
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  alertCircleOutline,
  chatbubbleOutline,
  cloudOfflineOutline,
  constructOutline,
  locationOutline,
  peopleOutline,
  personCircleOutline,
  pricetagOutline,
  star,
} from 'ionicons/icons';
import { catchError, forkJoin, of } from 'rxjs';

import type { AnnouncementDetailsDto, RatingDto, UserPublicDto } from '../../../core/api/api-client';
import { AnnouncementsClient, UsersClient } from '../../../core/api/api-client';
import { errorStatus } from '../../../core/api/api-errors';
import { apiResourceUrl } from '../../../core/api/api.providers';
import { loginUrlTree } from '../../../core/auth/auth.guard';
import { AuthService } from '../../../core/auth/auth.service';
import { CategoriesService } from '../../../core/categories/categories.service';
import { openStreetMapUrl } from '../../../core/config/map';
import { ToastService } from '../../../core/ui/toast.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import type { MapMarker } from '../../../shared/components/map/map.component';
import { MapComponent } from '../../../shared/components/map/map.component';
import { PricePipe } from '../../../shared/pipes/price.pipe';
import { TimeAgoPipe, parseApiDate } from '../../../shared/pipes/time-ago.pipe';
import { pluralPl } from '../../../shared/text/plural';
import { difficultyLabel, statusColor, statusLabel } from '../announcement-labels';

type LoadState = 'loading' | 'ready' | 'not-found' | 'failed';

/** Ile ostatnich opinii o właścicielu pokazujemy pod ogłoszeniem. */
const VISIBLE_RATINGS = 3;

/**
 * Szczegóły ogłoszenia (publiczne): zdjęcia, opis, mapa, właściciel z ocenami i akcje „Złóż ofertę” / „Napisz”
 * (niezalogowany → logowanie i powrót tutaj). Oferty i rozmowy – Krok F5.
 */
@Component({
  selector: 'app-announcement-details',
  templateUrl: './announcement-details.page.html',
  styleUrls: ['./announcement-details.page.scss'],
  imports: [
    DatePipe,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonSkeletonText,
    IonBadge,
    IonText,
    IonList,
    IonListHeader,
    IonItem,
    IonIcon,
    IonLabel,
    IonNote,
    IonButton,
    IonFooter,
    EmptyStateComponent,
    MapComponent,
    PricePipe,
    TimeAgoPipe,
  ],
})
export class AnnouncementDetailsPage implements ViewWillEnter {
  private readonly announcements = inject(AnnouncementsClient);
  private readonly users = inject(UsersClient);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthService);
  protected readonly categories = inject(CategoriesService);

  /** Z adresu `/announcements/:announcementId` (`withComponentInputBinding`). */
  readonly announcementId = input.required<string>();

  protected readonly state = signal<LoadState>('loading');
  protected readonly announcement = signal<AnnouncementDetailsDto | null>(null);
  protected readonly owner = signal<UserPublicDto | null>(null);
  protected readonly ownerRatings = signal<RatingDto[]>([]);

  protected readonly parseApiDate = parseApiDate;
  protected readonly difficultyLabel = difficultyLabel;
  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;

  protected readonly images = computed(() =>
    (this.announcement()?.images ?? []).map((image) => ({ id: image.id, url: apiResourceUrl(image.url) ?? '' })),
  );
  protected readonly category = computed(() => {
    const announcement = this.announcement();
    return announcement
      ? [this.categories.name(announcement.categoryId), this.categories.name(announcement.subcategoryId)]
          .filter(Boolean)
          .join(' · ')
      : '';
  });
  protected readonly marker = computed<MapMarker[]>(() => {
    const announcement = this.announcement();
    return announcement ? [{ id: announcement.id, ...announcement.location }] : [];
  });
  protected readonly mapLink = computed(() => {
    const announcement = this.announcement();
    return announcement ? openStreetMapUrl(announcement.location) : '';
  });
  protected readonly isOwner = computed(() => {
    const user = this.auth.currentUser();
    return user !== null && user.id === this.announcement()?.ownerId;
  });
  protected readonly isOpen = computed(() => this.announcement()?.status === 'Open');
  protected readonly ratingSummary = computed(() => {
    const ratings = this.ownerRatings();
    if (ratings.length === 0) {
      return null;
    }
    const average = ratings.reduce((sum, rating) => sum + rating.rate, 0) / ratings.length;
    return {
      average: average.toLocaleString('pl-PL', { minimumFractionDigits: 1, maximumFractionDigits: 1 }),
      count: `${ratings.length} ${pluralPl(ratings.length, 'ocena', 'oceny', 'ocen')}`,
    };
  });
  /** Najnowsze opinie z komentarzem. */
  protected readonly recentReviews = computed(() =>
    this.ownerRatings()
      .filter((rating) => !!rating.comment)
      .sort((a, b) => parseApiDate(b.createdAt).getTime() - parseApiDate(a.createdAt).getTime())
      .slice(0, VISIBLE_RATINGS),
  );

  constructor() {
    addIcons({
      alertCircleOutline,
      chatbubbleOutline,
      cloudOfflineOutline,
      constructOutline,
      locationOutline,
      peopleOutline,
      personCircleOutline,
      pricetagOutline,
      star,
    });
  }

  /** Przy każdym wejściu (także po powrocie z logowania – zalogowany może widzieć swoje oferty). */
  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    if (this.auth.isAuthenticated() && !this.auth.currentUser()) {
      this.auth.loadCurrentUser().subscribe({ error: () => undefined });
    }
    this.load();
  }

  refresh(event: Event): void {
    this.load(() => void (event.target as unknown as { complete(): Promise<void> }).complete());
  }

  load(done?: () => void): void {
    this.announcements.getById(this.announcementId()).subscribe({
      next: (announcement) => {
        const ownerChanged = this.announcement()?.ownerId !== announcement.ownerId;
        this.announcement.set(announcement);
        this.state.set('ready');
        if (ownerChanged) {
          this.loadOwner(announcement.ownerId);
        }
        done?.();
      },
      error: (error: unknown) => {
        if (errorStatus(error) === 404) {
          this.announcement.set(null);
          this.state.set('not-found');
        } else if (!this.announcement()) {
          this.state.set('failed');
        }
        done?.();
      },
    });
  }

  /** „Złóż ofertę” / „Napisz”: niezalogowany → logowanie i powrót do tego ogłoszenia. */
  makeOffer(): void {
    // TODO(F5): formularz oferty.
    if (this.requireSignIn()) {
      void this.toast.info('Składanie ofert pojawi się w kolejnej wersji aplikacji.');
    }
  }

  writeMessage(): void {
    // TODO(F5): wątek rozmowy (`POST …/conversations`).
    if (this.requireSignIn()) {
      void this.toast.info('Rozmowy z właścicielem pojawią się w kolejnej wersji aplikacji.');
    }
  }

  private requireSignIn(): boolean {
    if (this.auth.isAuthenticated()) {
      return true;
    }
    void this.router.navigateByUrl(loginUrlTree(this.router, this.router.url));
    return false;
  }

  /** Właściciel i jego oceny – dodatki: błąd nie psuje ekranu (komunikat pokazał interceptor). */
  private loadOwner(ownerId: string): void {
    this.owner.set(null);
    this.ownerRatings.set([]);
    forkJoin({
      owner: this.users.getById(ownerId).pipe(catchError(() => of(null))),
      ratings: this.users.getRatings(ownerId).pipe(catchError(() => of<RatingDto[]>([]))),
    }).subscribe(({ owner, ratings }) => {
      this.owner.set(owner);
      this.ownerRatings.set(ratings);
    });
  }
}
