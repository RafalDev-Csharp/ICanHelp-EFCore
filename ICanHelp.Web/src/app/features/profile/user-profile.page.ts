import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
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
  IonListHeader,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  alertCircleOutline,
  chatbubbleEllipsesOutline,
  checkmarkDoneOutline,
  cloudOfflineOutline,
  exitOutline,
  personCircleOutline,
  star,
  stopCircleOutline,
} from 'ionicons/icons';

import type { ProfileInterruptionDto, RoleStatsDto, UserProfileDto } from '../../core/api/api-client';
import { UsersClient } from '../../core/api/api-client';
import { errorStatus } from '../../core/api/api-errors';
import { APP_LOCALE } from '../../core/config/locale';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { parseApiDate } from '../../shared/pipes/time-ago.pipe';
import { pluralPl } from '../../shared/text/plural';
import { jobExitReasonText } from '../offers/job-exit';
import { formatDateTime } from '../offers/offer-labels';

type LoadState = 'loading' | 'ready' | 'not-found' | 'failed';
/** Segment statystyk: rola w zleceniach. */
export type ProfileRole = 'Helper' | 'Client';

/** „4,65” – średnia z dwoma miejscami po przecinku. */
const averageFormat = new Intl.NumberFormat(APP_LOCALE, { minimumFractionDigits: 1, maximumFractionDigits: 2 });

/** Wiersz statystyk do szablonu. */
export interface StatRow {
  label: string;
  value: string;
  /** Wyróżnienie (ostrzeżenie) – sam przerywał / rezygnował. */
  warn: boolean;
}

/** Statystyki jednej roli w wierszach („Zakończone zlecenia: 14”, „Przerwane przez niego: 1”…). */
export function statRows(stats: RoleStatsDto, role: ProfileRole): StatRow[] {
  const other = role === 'Helper' ? 'zleceniodawcę' : 'pomocnika';
  return [
    { label: 'Zakończone zlecenia', value: String(stats.completedCount), warn: false },
    { label: 'Przerwane przez niego', value: String(stats.interruptedByUser), warn: stats.interruptedByUser > 0 },
    { label: `Przerwane przez ${other}`, value: String(stats.interruptedByOther), warn: false },
    { label: 'Rezygnacje przed startem', value: String(stats.abandonedByUser), warn: stats.abandonedByUser > 0 },
    { label: `Rezygnacje – ${other}`, value: String(stats.abandonedByOther), warn: false },
  ];
}

/** „★ 4,65 · 12 ocen” albo „Jeszcze bez ocen” (średnia tylko z zakończonych zleceń). */
export function ratingLabel(stats: RoleStatsDto): string {
  if (stats.averageRating === undefined || stats.averageRating === null || stats.ratingCount === 0) {
    return 'Jeszcze bez ocen';
  }
  const count = `${stats.ratingCount} ${pluralPl(stats.ratingCount, 'ocena', 'oceny', 'ocen')}`;
  return `${averageFormat.format(stats.averageRating)} / 5 · ${count}`;
}

/**
 * Publiczny profil (`/users/:userId`, F11 G, także bez logowania): dane publiczne, statystyki osobno „Pomagam” /
 * „Zlecam” (`GET api/users/{id}/profile` – oceny tylko z zakończonych zleceń, zakończone, przerwane i rezygnacje: przez
 * niego / przez drugą stronę) i „Zlecenia przerwane” – rola, kto przerwał, powód i stanowisko drugiej strony.
 * Serwis niczego nie rozstrzyga – pokazuje obie wersje.
 */
@Component({
  selector: 'app-user-profile',
  templateUrl: './user-profile.page.html',
  styleUrls: ['./user-profile.page.scss'],
  imports: [
    BrandComponent,
    DatePipe,
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
    IonSpinner,
    IonSegment,
    IonSegmentButton,
    IonList,
    IonListHeader,
    IonItem,
    IonIcon,
    IonLabel,
    IonNote,
    IonButton,
  ],
})
export class UserProfilePage implements ViewWillEnter {
  private readonly users = inject(UsersClient);

  /** Z adresu `/users/:userId` (`withComponentInputBinding`). */
  readonly userId = input.required<string>();

  readonly state = signal<LoadState>('loading');
  readonly profile = signal<UserProfileDto | null>(null);
  readonly role = signal<ProfileRole>('Helper');

  protected readonly parseApiDate = parseApiDate;
  protected readonly formatDateTime = formatDateTime;
  protected readonly reasonText = jobExitReasonText;

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
      cloudOfflineOutline,
      exitOutline,
      personCircleOutline,
      star,
      stopCircleOutline,
    });
  }

  ionViewWillEnter(): void {
    this.load();
  }

  refresh(event: Event): void {
    this.load(() => void (event.target as unknown as { complete(): Promise<void> }).complete());
  }

  load(done?: () => void): void {
    this.users.getProfile(this.userId()).subscribe({
      next: (profile) => {
        const first = this.profile()?.user.id !== profile.user.id;
        this.profile.set(profile);
        this.state.set('ready');
        if (first) {
          // Na start rola, w której ma więcej zleceń.
          const helperJobs = profile.asHelper.completedCount + profile.asHelper.interruptedByUser;
          const clientJobs = profile.asClient.completedCount + profile.asClient.interruptedByUser;
          this.role.set(clientJobs > helperJobs ? 'Client' : 'Helper');
        }
        done?.();
      },
      error: (error: unknown) => {
        this.state.set(errorStatus(error) === 404 ? 'not-found' : this.profile() ? 'ready' : 'failed');
        done?.();
      },
    });
  }

  selectRole(value: unknown): void {
    if (value === 'Helper' || value === 'Client') {
      this.role.set(value);
    }
  }

  /** „Pomagał · przerwał sam” / „Zlecał · przerwał pomocnik”. */
  protected interruptionMeta(item: ProfileInterruptionDto): string {
    const role = item.role === 'Helper' ? 'Pomagał' : 'Zlecał';
    const who = item.byUser ? 'przerwał sam' : item.role === 'Helper' ? 'przerwał zleceniodawca' : 'przerwał pomocnik';
    return `${role} · ${who}`;
  }

  /** Kto napisał stanowisko: druga strona niż ta, która przerwała. */
  protected responseAuthor(item: ProfileInterruptionDto): string {
    return item.exit.actorRole === 'Client' ? 'Stanowisko pomocnika' : 'Stanowisko zleceniodawcy';
  }
}
