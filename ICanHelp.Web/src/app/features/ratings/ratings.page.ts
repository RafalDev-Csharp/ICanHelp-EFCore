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
  IonThumbnail,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { imageOutline, starOutline } from 'ionicons/icons';
import { forkJoin } from 'rxjs';

import type { MyRatingDto, PendingRatingDto } from '../../core/api/api-client';
import { MyRatingsClient } from '../../core/api/api-client';
import { apiResourceUrl } from '../../core/api/api.providers';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { runExclusive } from '../../shared/data/exclusive';
import { RemoteData } from '../../shared/data/remote-item';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { JobService } from '../offers/job.service';

import { groupByRater, groupedAverage, raterGroupLabel, ratingsSummary } from './rating-groups';
import { RatingStarsComponent } from './rating-stars.component';

/** Segment: do wystawienia, otrzymane jako pomocnik, otrzymane jako zleceniodawca. */
export type RatingsTab = 'pending' | 'Helper' | 'Client';

interface MyRatings {
  received: MyRatingDto[];
  pending: PendingRatingDto[];
}

/**
 * „Oceny” w Koncie (`/profile/ratings`, F11 F): segment „Do wystawienia” (zakończone zlecenia bez mojej oceny →
 * „Oceń” – `JobService.rate`, to samo okno co w szczegółach ogłoszenia) · „Jako pomocnik” · „Jako zleceniodawca”
 * (otrzymane oceny ze średnią; F11 H: oceny od tej samej osoby zgrupowane – „ocena grupowa” = ich średnia, jeden głos
 * w średniej – `rating-groups.ts`). Na start – „Do wystawienia”, jeśli coś czeka, inaczej „Jako pomocnik”.
 */
@Component({
  selector: 'app-ratings',
  templateUrl: './ratings.page.html',
  styleUrls: ['./ratings.page.scss'],
  imports: [
    BrandComponent,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
    RatingStarsComponent,
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
  private readonly jobs = inject(JobService);

  private readonly ratings = new RemoteData<MyRatings>(
    () => forkJoin({ received: this.client.getMine(), pending: this.client.getPending() }),
    () => {
      if (!this.tabChosen) {
        this.tab.set(this.pending().length > 0 ? 'pending' : 'Helper');
      }
    },
  );
  readonly state = this.ratings.state;
  readonly received = computed(() => this.ratings.value()?.received ?? []);
  readonly pending = computed(() => this.ratings.value()?.pending ?? []);
  readonly busy = signal(false);
  readonly tab = signal<RatingsTab>('pending');
  /** Segment wybrany przez użytkownika – kolejne wczytania go nie zmieniają. */
  private tabChosen = false;

  protected readonly thumbnail = apiResourceUrl;
  protected readonly groupLabel = raterGroupLabel;

  /** Otrzymane oceny w wybranej roli: zgrupowane po osobie, ze średnią (jeden głos na osobę) i podsumowaniem. */
  protected readonly receivedView = computed(() => {
    const role = this.tab() === 'Client' ? 'Client' : 'Helper';
    const groups = groupByRater(this.received().filter((rating) => rating.myRole === role));
    return { groups, average: groupedAverage(groups), summary: ratingsSummary(groups) };
  });

  constructor() {
    addIcons({ imageOutline, starOutline });
  }

  ionViewWillEnter(): void {
    this.load();
  }

  refresh(event: Event): void {
    this.ratings.refresh(event);
  }

  load(): void {
    this.ratings.load();
  }

  selectTab(value: unknown): void {
    if (value === 'pending' || value === 'Helper' || value === 'Client') {
      this.tab.set(value);
      this.tabChosen = true;
    }
  }

  /** „Oceń” – okno oceny; po wysłaniu zlecenie znika z „Do wystawienia”. */
  async rate(item: PendingRatingDto): Promise<void> {
    await runExclusive(this.busy, async () => {
      if (await this.jobs.rate(item.announcementId, item.otherParty.userName)) {
        this.load();
      }
    });
  }
}
