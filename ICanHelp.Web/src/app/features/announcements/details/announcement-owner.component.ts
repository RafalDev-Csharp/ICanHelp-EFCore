import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IonIcon, IonItem, IonLabel, IonList, IonListHeader, IonSkeletonText, IonText } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { personCircleOutline, star, warningOutline } from 'ionicons/icons';

import type { RatingDto, UserPublicDto } from '../../../core/api/api-client';
import { TimeAgoPipe, parseApiDate } from '../../../shared/pipes/time-ago.pipe';
import { formatAverage, ratingsCountLabel } from '../../../shared/text/rating-texts';

/** Ile ostatnich opinii o właścicielu pokazujemy pod ogłoszeniem. */
const VISIBLE_RATINGS = 3;

/**
 * Karta „Zleceniodawca” w szczegółach ogłoszenia: kto wystawił (→ publiczny profil), licznik rezygnacji / przerwań,
 * średnia ocen i najnowsze opinie z komentarzem. Do czasu wczytania właściciela – szkielet.
 */
@Component({
  selector: 'app-announcement-owner',
  template: `
    <ion-list inset="true" class="owner">
      <ion-list-header>
        <ion-label>Zleceniodawca</ion-label>
      </ion-list-header>
      @if (owner(); as person) {
        <ion-item class="owner-profile" [routerLink]="['/users', person.id]" [detail]="true">
          <ion-icon name="person-circle-outline" slot="start" aria-hidden="true"></ion-icon>
          <ion-label>
            <h3>{{ person.fullName }}</h3>
            <p>{{ person.city }} · w serwisie od {{ parseApiDate(person.createdAt) | date: 'MMMM yyyy' }}</p>
          </ion-label>
        </ion-item>
        @if (reliability(); as counts) {
          <ion-item class="reliability">
            <ion-icon name="warning-outline" slot="start" color="warning" aria-hidden="true"></ion-icon>
            <ion-label class="ion-text-wrap">{{ counts }}</ion-label>
          </ion-item>
        }
        <ion-item [lines]="recentReviews().length > 0 ? 'inset' : 'none'">
          <ion-icon name="star" slot="start" color="warning" aria-hidden="true"></ion-icon>
          @if (ratingSummary(); as rating) {
            <ion-label>
              <strong>{{ rating.average }}</strong> / 5
              <ion-text color="medium"> · {{ rating.count }}</ion-text>
            </ion-label>
          } @else {
            <ion-label color="medium">Jeszcze bez ocen</ion-label>
          }
        </ion-item>
        @for (review of recentReviews(); track review.id; let last = $last) {
          <ion-item [lines]="last ? 'none' : 'inset'">
            <ion-label class="ion-text-wrap">
              <p class="review-rate" [attr.aria-label]="'Ocena ' + review.rate + ' na 5'">
                {{ '★'.repeat(review.rate) }}<span class="review-rate-empty">{{ '★'.repeat(5 - review.rate) }}</span>
                <ion-text color="medium"> · {{ review.createdAt | timeAgo }}</ion-text>
              </p>
              <p class="review-text">{{ review.comment }}</p>
            </ion-label>
          </ion-item>
        }
      } @else {
        <ion-item lines="none">
          <ion-label>
            <ion-skeleton-text [animated]="true" style="width: 50%"></ion-skeleton-text>
            <ion-skeleton-text [animated]="true" style="width: 30%"></ion-skeleton-text>
          </ion-label>
        </ion-item>
      }
    </ion-list>
  `,
  styles: `
    :host {
      display: contents;
    }
    .review-rate {
      color: var(--ion-color-warning);
    }
    .review-rate-empty {
      color: var(--ion-color-light-shade);
    }
    .review-text {
      color: var(--ion-text-color);
      white-space: pre-line;
    }
  `,
  imports: [
    DatePipe,
    RouterLink,
    IonList,
    IonListHeader,
    IonItem,
    IonIcon,
    IonLabel,
    IonText,
    IonSkeletonText,
    TimeAgoPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnnouncementOwnerComponent {
  /** `null` – jeszcze się wczytuje (albo się nie udało – zostaje szkielet). */
  readonly owner = input.required<UserPublicDto | null>();
  /** Wszystkie oceny właściciela (`GET api/users/{id}/ratings`). */
  readonly ratings = input.required<RatingDto[]>();
  /** „1 rezygnacja · 2 przerwane zlecenia”; pusty = brak. */
  readonly reliability = input('');

  protected readonly parseApiDate = parseApiDate;

  protected readonly ratingSummary = computed(() => {
    const ratings = this.ratings();
    if (ratings.length === 0) {
      return null;
    }
    const average = ratings.reduce((sum, rating) => sum + rating.rate, 0) / ratings.length;
    return { average: formatAverage(average), count: ratingsCountLabel(ratings.length) };
  });
  /** Najnowsze opinie z komentarzem. */
  protected readonly recentReviews = computed(() =>
    this.ratings()
      .filter((rating) => !!rating.comment)
      .sort((a, b) => parseApiDate(b.createdAt).getTime() - parseApiDate(a.createdAt).getTime())
      .slice(0, VISIBLE_RATINGS),
  );

  constructor() {
    addIcons({ personCircleOutline, star, warningOutline });
  }
}
