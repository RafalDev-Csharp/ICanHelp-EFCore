import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonList,
  IonRefresher,
  IonRefresherContent,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { heartOutline } from 'ionicons/icons';
import type { Observable } from 'rxjs';
import { catchError, forkJoin, map, of, switchMap } from 'rxjs';

import type { AnnouncementDto } from '../../core/api/api-client';
import { AnnouncementsClient } from '../../core/api/api-client';
import { CategoriesService } from '../../core/categories/categories.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { NotificationsBellComponent } from '../../shared/components/notifications-bell/notifications-bell.component';
import { RemoteList } from '../../shared/data/remote-list';
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
    IonButton,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
    AnnouncementCardComponent,
  ],
})
export class LikedAnnouncementsPage implements ViewWillEnter {
  private readonly client = inject(AnnouncementsClient);
  private readonly categories = inject(CategoriesService);
  protected readonly likes = inject(LikesService);

  protected readonly list = new RemoteList<AnnouncementDto>(() => this.fetchLiked());
  /** Odklikane serduszko w szczegółach znika z listy od razu (bez ponownego wczytania). */
  readonly items = computed(() => this.list.items().filter((item) => this.likes.isLiked(item.id)));

  constructor() {
    addIcons({ heartOutline });
  }

  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    this.list.reload();
  }

  /** Polubienia od najnowszego → szczegóły każdego ogłoszenia (usunięte albo niedostępne pomijamy). */
  private fetchLiked(): Observable<AnnouncementDto[]> {
    return this.likes.load(true).pipe(
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
    );
  }
}
