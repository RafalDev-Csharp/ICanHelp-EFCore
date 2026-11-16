import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  alertCircleOutline,
  chatbubbleEllipsesOutline,
  checkmarkDoneOutline,
  personCircleOutline,
  star,
  stopCircleOutline,
} from 'ionicons/icons';

import type { UserProfileDto } from '../../core/api/api-client';
import { UsersClient } from '../../core/api/api-client';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { RemoteItem } from '../../shared/data/remote-item';
import { parseApiDate } from '../../shared/pipes/time-ago.pipe';
import { jobExitReasonText, jobExitResponseAuthor } from '../offers/job-exit';
import { formatDateTime } from '../offers/offer-labels';

import type { ProfileRole } from './profile-stats';
import { defaultProfileRole, interruptionMeta, ratingLabel, statRows } from './profile-stats';

/**
 * Publiczny profil (`/users/:userId`, F11 G, także bez logowania): dane publiczne, statystyki osobno „Pomagam” /
 * „Zlecam” (`GET api/users/{id}/profile` – oceny tylko z zakończonych zleceń, zakończone, przerwane i rezygnacje: przez
 * niego / przez drugą stronę) i „Zlecenia przerwane” – rola, kto przerwał, powód i stanowisko drugiej strony.
 * Serwis niczego nie rozstrzyga – pokazuje obie wersje. Teksty statystyk – `profile-stats.ts`.
 */
@Component({
  selector: 'app-user-profile',
  templateUrl: './user-profile.page.html',
  styleUrls: ['./user-profile.page.scss'],
  imports: [
    BrandComponent,
    DatePipe,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
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
    IonListHeader,
    IonItem,
    IonIcon,
    IonLabel,
    IonNote,
  ],
})
export class UserProfilePage implements ViewWillEnter {
  private readonly users = inject(UsersClient);

  /** Z adresu `/users/:userId` (`withComponentInputBinding`). */
  readonly userId = input.required<string>();

  protected readonly resource = new RemoteItem<UserProfileDto>(
    () => this.users.getProfile(this.userId()),
    (profile, previous) => {
      if (previous?.user.id !== profile.user.id) {
        this.role.set(defaultProfileRole(profile));
      }
    },
  );
  readonly state = this.resource.state;
  readonly profile = this.resource.value;
  readonly role = signal<ProfileRole>('Helper');

  protected readonly parseApiDate = parseApiDate;
  protected readonly formatDateTime = formatDateTime;
  protected readonly reasonText = jobExitReasonText;
  protected readonly interruptionMeta = interruptionMeta;
  /** Kto napisał stanowisko: druga strona niż ta, która przerwała. */
  protected readonly responseAuthor = jobExitResponseAuthor;

  readonly stats = computed(() => {
    const profile = this.profile();
    return profile ? (this.role() === 'Helper' ? profile.asHelper : profile.asClient) : null;
  });
  readonly rows = computed(() => {
    const stats = this.stats();
    return stats ? statRows(stats, this.role()) : [];
  });
  readonly rating = computed(() => {
    const stats = this.stats();
    return stats ? ratingLabel(stats) : '';
  });

  constructor() {
    addIcons({
      alertCircleOutline,
      chatbubbleEllipsesOutline,
      checkmarkDoneOutline,
      personCircleOutline,
      star,
      stopCircleOutline,
    });
  }

  ionViewWillEnter(): void {
    this.resource.load();
  }

  selectRole(value: unknown): void {
    if (value === 'Helper' || value === 'Client') {
      this.role.set(value);
    }
  }
}
