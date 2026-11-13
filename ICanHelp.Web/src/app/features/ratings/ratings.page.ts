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

import type { MyRatingDto, PendingRatingDto } from '../../core/api/api-client';
import { MyRatingsClient } from '../../core/api/api-client';
import { apiResourceUrl } from '../../core/api/api.providers';
import { APP_LOCALE } from '../../core/config/locale';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { pluralPl } from '../../shared/text/plural';
import { OffersService } from '../offers/offers.service';

type LoadState = 'loading' | 'ready' | 'failed';
/** Segment: do wystawienia, otrzymane jako pomocnik, otrzymane jako zleceniodawca. */
export type RatingsTab = 'pending' | 'Helper' | 'Client';

const STARS = [1, 2, 3, 4, 5] as const;

/** „4,5” – średnia z jednym miejscem po przecinku (pl-PL). */
const averageFormat = new Intl.NumberFormat(APP_LOCALE, { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/** F11 H: oceny od jednej osoby – „ocena grupowa” = średnia jej ocen (2 i 4 → 3), jeden głos w średniej. */
export interface RaterGroup {
  raterId: string;
  userName: string;
  /** Najnowsza pierwsza. */
  ratings: MyRatingDto[];
  average: number;
}

function mean(values: readonly number[]): number {
  return values.length === 0 ? 0 : values.reduce((sum, value) => sum + value, 0) / values.length;
}

/** Grupuje oceny po oceniającym; grupy w kolejności najnowszej oceny (lista z API jest od najnowszych). */
export function groupByRater(ratings: readonly MyRatingDto[]): RaterGroup[] {
  const groups = new Map<string, RaterGroup>();
  for (const rating of ratings) {
    const group = groups.get(rating.rater.id);
    if (group) {
      group.ratings.push(rating);
    } else {
      groups.set(rating.rater.id, {
        raterId: rating.rater.id,
        userName: rating.rater.userName,
        ratings: [rating],
        average: 0,
      });
    }
  }
  return [...groups.values()].map((group) => ({ ...group, average: mean(group.ratings.map((r) => r.rate)) }));
}

/** Średnia „jedna osoba = jeden głos”: średnia z ocen grupowych (jak na profilu, bez reguł tempa – te liczy API). */
export function groupedAverage(groups: readonly RaterGroup[]): number {
  return mean(groups.map((group) => group.average));
}

/**
 * „Oceny” w Koncie (`/profile/ratings`, F11 F): segment „Do wystawienia” (zakończone zlecenia bez mojej oceny →
 * „Oceń” – `OffersService.rate`, to samo okno co w szczegółach ogłoszenia) · „Jako pomocnik” · „Jako zleceniodawca”
 * (otrzymane oceny ze średnią; F11 H: oceny od tej samej osoby zgrupowane – „ocena grupowa” = ich średnia, jeden głos
 * w średniej). Na start – „Do wystawienia”, jeśli coś czeka, inaczej „Jako pomocnik”.
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

  /** „4,0 · 3 oceny od 2 osób” – podsumowanie (jedna osoba = jeden głos; „od N osób”, gdy mniej niż ocen). */
  summary(ratings: readonly MyRatingDto[]): string {
    if (ratings.length === 0) {
      return '';
    }
    const groups = groupByRater(ratings);
    const count = ratings.length;
    const people =
      groups.length < count ? ` od ${groups.length} ${pluralPl(groups.length, 'osoby', 'osób', 'osób')}` : '';
    return `${averageFormat.format(groupedAverage(groups))} · ${count} ${pluralPl(count, 'ocena', 'oceny', 'ocen')}${people}`;
  }

  /** „Ocena grupowa · 2 oceny, średnio 3,0”. */
  groupLabel(group: RaterGroup): string {
    const count = group.ratings.length;
    return `Ocena grupowa · ${count} ${pluralPl(count, 'ocena', 'oceny', 'ocen')}, średnio ${averageFormat.format(group.average)}`;
  }

  /** Ikona gwiazdki `index` (1–5) dla oceny `value` (średnia może mieć połówkę). */
  starIcon(index: number, value: number): string {
    if (value >= index) {
      return 'star';
    }
    return value >= index - 0.5 ? 'star-half' : 'star-outline';
  }

  /** Średnia do gwiazdek podsumowania – ta sama co w `summary` (jeden głos na osobę). */
  average(ratings: readonly MyRatingDto[]): number {
    return groupedAverage(groupByRater(ratings));
  }

  protected readonly groupByRater = groupByRater;
}
