import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonSpinner,
  IonThumbnail,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { cloudOfflineOutline, imageOutline, star, starHalf, starOutline } from 'ionicons/icons';
import { forkJoin } from 'rxjs';

import type { JobExitDto, MyRatingDto, PendingRatingDto } from '../../core/api/api-client';
import { MyRatingsClient } from '../../core/api/api-client';
import { apiResourceUrl } from '../../core/api/api.providers';
import { APP_LOCALE } from '../../core/config/locale';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { pluralPl } from '../../shared/text/plural';
import { jobExitReasonText } from '../offers/job-exit';
import { partyLabel } from '../offers/offer-labels';
import { OffersService } from '../offers/offers.service';

type LoadState = 'loading' | 'ready' | 'failed';
/** Segment: do wystawienia, otrzymane jako pomocnik, otrzymane jako zleceniodawca. */
export type RatingsTab = 'pending' | 'Helper' | 'Client';

const STARS = [1, 2, 3, 4, 5] as const;

/** „4,5” – średnia z jednym miejscem po przecinku (pl-PL). */
const averageFormat = new Intl.NumberFormat(APP_LOCALE, { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/**
 * „Oceny” w Koncie (`/profile/ratings`, F11 F): segment „Do wystawienia” (zakończone zlecenia bez mojej oceny →
 * „Oceń” – `OffersService.rate`, to samo okno co w szczegółach ogłoszenia) · „Jako pomocnik” · „Jako zleceniodawca”
 * (otrzymane oceny ze średnią). Na start – „Do wystawienia”, jeśli coś czeka, inaczej „Jako pomocnik”.
 */
@Component({
  selector: 'app-ratings',
  templateUrl: './ratings.page.html',
  styleUrls: ['./ratings.page.scss'],
  imports: [
    BrandComponent,
    EmptyStateComponent,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonSegment,
    IonSegmentButton,
    IonSpinner,
    IonList,
    IonItem,
    IonLabel,
    IonNote,
    IonIcon,
    IonThumbnail,
    IonButton,
    TimeAgoPipe,
  ],
})
export class RatingsPage implements ViewWillEnter {
  private readonly client = inject(MyRatingsClient);
  private readonly offers = inject(OffersService);

  readonly state = signal<LoadState>('loading');
  readonly received = signal<MyRatingDto[]>([]);
  readonly pending = signal<PendingRatingDto[]>([]);
  readonly busy = signal(false);
  readonly tab = signal<RatingsTab>('pending');
  /** Segment wybrany przez użytkownika – kolejne wczytania go nie zmieniają. */
  private tabChosen = false;

  protected readonly stars = STARS;
  protected readonly thumbnail = apiResourceUrl;

  /** F11 E: kto przerwał i dlaczego – „Pomocnik: Brak kontaktu” (przy ocenie przerwanego zlecenia). */
  protected exitText(exit: JobExitDto): string {
    return `${partyLabel(exit.actorRole)}: ${jobExitReasonText(exit)}`;
  }

  readonly asHelper = computed(() => this.received().filter((rating) => rating.myRole === 'Helper'));
  readonly asClient = computed(() => this.received().filter((rating) => rating.myRole === 'Client'));

  constructor() {
    addIcons({ cloudOfflineOutline, imageOutline, star, starHalf, starOutline });
  }

  ionViewWillEnter(): void {
    this.load();
  }

  refresh(event: Event): void {
    this.load(() => void (event.target as unknown as { complete(): Promise<void> }).complete());
  }

  load(done?: () => void): void {
    forkJoin({ received: this.client.getMine(), pending: this.client.getPending() }).subscribe({
      next: ({ received, pending }) => {
        this.received.set(received ?? []);
        this.pending.set(pending ?? []);
        if (!this.tabChosen) {
          this.tab.set(this.pending().length > 0 ? 'pending' : 'Helper');
        }
        this.state.set('ready');
        done?.();
      },
      error: () => {
        if (this.state() !== 'ready') {
          this.state.set('failed');
        }
        done?.();
      },
    });
  }

  selectTab(value: unknown): void {
    if (value === 'pending' || value === 'Helper' || value === 'Client') {
      this.tab.set(value);
      this.tabChosen = true;
    }
  }

  /** „Oceń” – okno oceny; po wysłaniu zlecenie znika z „Do wystawienia”. */
  async rate(item: PendingRatingDto): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      if (await this.offers.rate(item.announcementId, item.otherParty.userName)) {
        this.load();
      }
    } finally {
      this.busy.set(false);
    }
  }

  /** „4,5 · 2 oceny” – podsumowanie listy otrzymanych ocen. */
  summary(ratings: readonly MyRatingDto[]): string {
    if (ratings.length === 0) {
      return '';
    }
    const count = ratings.length;
    return `${averageFormat.format(this.average(ratings))} · ${count} ${pluralPl(count, 'ocena', 'oceny', 'ocen')}`;
  }

  /** Ikona gwiazdki `index` (1–5) dla oceny `value` (średnia może mieć połówkę). */
  starIcon(index: number, value: number): string {
    if (value >= index) {
      return 'star';
    }
    return value >= index - 0.5 ? 'star-half' : 'star-outline';
  }

  average(ratings: readonly MyRatingDto[]): number {
    return ratings.length === 0 ? 0 : ratings.reduce((sum, rating) => sum + rating.rate, 0) / ratings.length;
  }
}
