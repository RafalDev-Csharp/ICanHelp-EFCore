import { Component, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonInfiniteScroll,
  IonInfiniteScrollContent,
  IonItem,
  IonLabel,
  IonList,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  albumsOutline,
  arrowUndoOutline,
  chatbubbleEllipsesOutline,
  chatbubbleOutline,
  checkmarkCircleOutline,
  checkmarkDoneOutline,
  closeCircleOutline,
  createOutline,
  exitOutline,
  flagOutline,
  locationOutline,
  notificationsOutline,
  playOutline,
  pricetagOutline,
  refreshOutline,
  starOutline,
  stopCircleOutline,
  swapHorizontalOutline,
} from 'ionicons/icons';

import type { NotificationDto } from '../../core/api/api-client';
import { NotificationsClient } from '../../core/api/api-client';
import { asRead, isRead } from '../../core/notifications/notification-model';
import { describeNotification } from '../../core/notifications/notification-texts';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { PagedList } from '../../shared/data/remote-list';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';

/** Powiadomień na stronę (`GET api/me/notifications`). */
export const NOTIFICATIONS_PAGE_SIZE = 20;

/**
 * „Powiadomienia” (tylko zalogowani; z dzwonka w nagłówku albo z „Konta” – bez własnej zakładki): od najnowszych, nieprzeczytane wyróżnione, kliknięcie → właściwy
 * ekran (wątek / ogłoszenie) i przeczytane, „Oznacz wszystkie”. Nowe z huba dopisują się na górze bez odświeżania.
 */
@Component({
  selector: 'app-notifications',
  templateUrl: './notifications.page.html',
  styleUrls: ['./notifications.page.scss'],
  imports: [
    BrandComponent,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonBackButton,
    IonButton,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonList,
    IonItem,
    IonIcon,
    IonLabel,
    IonNote,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
    TimeAgoPipe,
  ],
})
export class NotificationsPage implements ViewWillEnter {
  private readonly client = inject(NotificationsClient);
  protected readonly notifications = inject(NotificationsService);

  protected readonly list = new PagedList<NotificationDto>(
    (page) => this.client.getMine(undefined, page, NOTIFICATIONS_PAGE_SIZE),
    (item) => item.id,
    NOTIFICATIONS_PAGE_SIZE,
  );
  readonly items = this.list.items;
  readonly hasUnread = computed(() => this.notifications.unreadCount() > 0 || this.items().some((n) => !isRead(n)));

  protected readonly describe = describeNotification;
  protected readonly isRead = isRead;

  constructor() {
    addIcons({
      albumsOutline,
      arrowUndoOutline,
      chatbubbleEllipsesOutline,
      chatbubbleOutline,
      checkmarkCircleOutline,
      checkmarkDoneOutline,
      closeCircleOutline,
      createOutline,
      exitOutline,
      flagOutline,
      locationOutline,
      notificationsOutline,
      playOutline,
      pricetagOutline,
      refreshOutline,
      starOutline,
      stopCircleOutline,
      swapHorizontalOutline,
    });

    // Nowe na górę; odnowiony skrót alertów (to samo id) przesuwa się na górę zamiast dublować.
    this.notifications.received$
      .pipe(takeUntilDestroyed())
      .subscribe((notification) =>
        this.items.update((current) => [notification, ...current.filter((n) => n.id !== notification.id)]),
      );
  }

  ionViewWillEnter(): void {
    this.list.reload();
    this.notifications.refreshCount();
  }

  refresh(event: Event): void {
    this.list.refresh(event);
    this.notifications.refreshCount();
  }

  async open(notification: NotificationDto): Promise<void> {
    this.items.update((current) => current.map((n) => (n.id === notification.id ? asRead(n) : n)));
    await this.notifications.open(notification);
  }

  async markAll(): Promise<void> {
    this.items.update((current) => current.map((n) => (isRead(n) ? n : asRead(n))));
    await this.notifications.markAllAsRead();
  }
}
