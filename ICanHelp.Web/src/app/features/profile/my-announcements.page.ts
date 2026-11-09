import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInfiniteScroll,
  IonInfiniteScrollContent,
  IonList,
  IonRefresher,
  IonRefresherContent,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { cloudOfflineOutline, documentTextOutline } from 'ionicons/icons';
import type { Observable } from 'rxjs';
import { of, switchMap } from 'rxjs';

import type { AnnouncementDto } from '../../core/api/api-client';
import { AnnouncementsClient } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import { CategoriesService } from '../../core/categories/categories.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { AnnouncementCardComponent } from '../announcements/list/announcement-card.component';
import { PAGE_SIZE } from '../announcements/list/announcement-filters';

interface CompletableTarget {
  complete(): Promise<void>;
}

/**
 * „Moje ogłoszenia” (profil, tylko zalogowani): wszystkie statusy, od najnowszych, ze statusem i liczbą ofert.
 * Edycja i anulowanie – w szczegółach ogłoszenia.
 */
@Component({
  selector: 'app-my-announcements',
  templateUrl: './my-announcements.page.html',
  imports: [
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonList,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    IonSpinner,
    IonButton,
    EmptyStateComponent,
    AnnouncementCardComponent,
  ],
})
export class MyAnnouncementsPage implements ViewWillEnter {
  private readonly client = inject(AnnouncementsClient);
  private readonly auth = inject(AuthService);
  private readonly categories = inject(CategoriesService);

  readonly items = signal<AnnouncementDto[]>([]);
  readonly state = signal<'loading' | 'ready' | 'failed'>('loading');
  readonly complete = signal(false);
  private page = 1;

  constructor() {
    addIcons({ documentTextOutline, cloudOfflineOutline });
  }

  /** Przy każdym wejściu – po edycji albo anulowaniu lista ma być aktualna. */
  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    this.reload();
  }

  reload(done?: () => void): void {
    if (this.items().length === 0) {
      this.state.set('loading');
    }
    this.fetch(1).subscribe({
      next: (items) => {
        this.page = 1;
        this.items.set(items);
        this.complete.set(items.length < PAGE_SIZE);
        this.state.set('ready');
        done?.();
      },
      error: () => {
        if (this.items().length === 0) {
          this.state.set('failed');
        }
        done?.();
      },
    });
  }

  refresh(event: Event): void {
    this.reload(() => void (event.target as unknown as CompletableTarget).complete());
  }

  loadMore(event: Event): void {
    const target = event.target as unknown as CompletableTarget;
    this.fetch(this.page + 1).subscribe({
      next: (items) => {
        this.page += 1;
        this.items.update((current) => [...current, ...items.filter((item) => !current.some((c) => c.id === item.id))]);
        this.complete.set(items.length < PAGE_SIZE);
        void target.complete();
      },
      error: () => void target.complete(),
    });
  }

  private fetch(page: number): Observable<AnnouncementDto[]> {
    const user = this.auth.currentUser();
    return (user ? of(user) : this.auth.loadCurrentUser()).pipe(
      switchMap((owner) =>
        this.client.getAll(
          owner?.id,
          undefined,
          undefined,
          undefined,
          undefined,
          undefined,
          undefined,
          page,
          PAGE_SIZE,
        ),
      ),
    );
  }
}
