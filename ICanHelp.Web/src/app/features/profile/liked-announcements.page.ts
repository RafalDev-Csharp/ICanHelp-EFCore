import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonList,
  IonRefresher,
  IonRefresherContent,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { cloudOfflineOutline, heartOutline } from 'ionicons/icons';
import { catchError, forkJoin, map, of, switchMap } from 'rxjs';

import type { AnnouncementDto } from '../../core/api/api-client';
import { AnnouncementsClient } from '../../core/api/api-client';
import { CategoriesService } from '../../core/categories/categories.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { NotificationsBellComponent } from '../../shared/components/notifications-bell/notifications-bell.component';
import { LikesService } from '../announcements/likes/likes.service';
import { AnnouncementCardComponent } from '../announcements/list/announcement-card.component';

/** Najwyżej tyle polubionych ogłoszeń wczytujemy naraz (każde to osobne żądanie). */
export const MAX_LIKED_SHOWN = 50;

/**
 * Zakładka „Ulubione” (`/liked`): ogłoszenia, którym użytkownik dał serduszko, od ostatnio polubionego.
 * API zwraca same id (`liked-announcements`), więc szczegóły dociągamy po jednym – przy tej skali wystarczy.
 */
@Component({
  selector: 'app-liked-announcements',
  templateUrl: './liked-announcements.page.html',
  imports: [
    BrandComponent,
    NotificationsBellComponent,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonList,
    IonSpinner,
    IonButton,
    EmptyStateComponent,
    AnnouncementCardComponent,
  ],
})
export class LikedAnnouncementsPage implements ViewWillEnter {
  private readonly client = inject(AnnouncementsClient);
  private readonly categories = inject(CategoriesService);
  protected readonly likes = inject(LikesService);

  private readonly loaded = signal<AnnouncementDto[]>([]);
  readonly state = signal<'loading' | 'ready' | 'failed'>('loading');
  /** Odklikane serduszko w szczegółach znika z listy od razu (bez ponownego wczytania). */
  readonly items = computed(() => this.loaded().filter((item) => this.likes.isLiked(item.id)));

  constructor() {
    addIcons({ heartOutline, cloudOfflineOutline });
  }

  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    this.reload();
  }

  reload(done?: () => void): void {
    if (this.loaded().length === 0) {
      this.state.set('loading');
    }
    this.likes
      .load(true)
      .pipe(
        switchMap((likes) => {
          const ids = [...likes]
            .sort((a, b) => b.likedAt.localeCompare(a.likedAt))
            .slice(0, MAX_LIKED_SHOWN)
            .map((like) => like.announcementId);
          return ids.length === 0
            ? of([])
            : forkJoin(ids.map((id) => this.client.getById(id).pipe(catchError(() => of(null)))));
        }),
        map((items) => items.filter((item): item is NonNullable<typeof item> => item !== null)),
      )
      .subscribe({
        next: (items) => {
          this.loaded.set(items);
          this.state.set('ready');
          done?.();
        },
        error: () => {
          if (this.loaded().length === 0) {
            this.state.set('failed');
          }
          done?.();
        },
      });
  }

  refresh(event: Event): void {
    this.reload(() => void (event.target as unknown as { complete(): Promise<void> }).complete());
  }
}
