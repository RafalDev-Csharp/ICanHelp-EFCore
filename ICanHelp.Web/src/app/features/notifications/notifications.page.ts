import { Component, computed, inject, signal } from '@angular/core';
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
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  albumsOutline,
  arrowUndoOutline,
  chatbubbleOutline,
  checkmarkCircleOutline,
  checkmarkDoneOutline,
  closeCircleOutline,
  cloudOfflineOutline,
  createOutline,
  locationOutline,
  notificationsOutline,
  pricetagOutline,
  refreshOutline,
  swapHorizontalOutline,
} from 'ionicons/icons';

import type { NotificationDto } from '../../core/api/api-client';
import { NotificationsClient } from '../../core/api/api-client';
import { describeNotification } from '../../core/notifications/notification-texts';
import { NotificationsService, asRead, isRead } from '../../core/notifications/notifications.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';

/** Powiadomień na stronę (`GET api/me/notifications`). */
export const NOTIFICATIONS_PAGE_SIZE = 20;

interface CompletableTarget {
  complete(): Promise<void>;
}

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
    IonSpinner,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    EmptyStateComponent,
    TimeAgoPipe,
  ],
})
export class NotificationsPage implements ViewWillEnter {
  private readonly client = inject(NotificationsClient);
  protected readonly notifications = inject(NotificationsService);

  readonly items = signal<NotificationDto[]>([]);
  readonly state = signal<'loading' | 'ready' | 'failed'>('loading');
  readonly complete = signal(false);
  readonly hasUnread = computed(() => this.notifications.unreadCount() > 0 || this.items().some((n) => !isRead(n)));
  private page = 1;

  protected readonly describe = describeNotification;
  protected readonly isRead = isRead;

  constructor() {
    addIcons({
      albumsOutline,
      arrowUndoOutline,
      chatbubbleOutline,
      checkmarkCircleOutline,
      checkmarkDoneOutline,
      closeCircleOutline,
      cloudOfflineOutline,
      createOutline,
      locationOutline,
      notificationsOutline,
      pricetagOutline,
      refreshOutline,
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
    this.reload();
    this.notifications.refreshCount();
  }

  reload(done?: () => void): void {
    if (this.items().length === 0) {
      this.state.set('loading');
    }
    this.client.getMine(undefined, 1, NOTIFICATIONS_PAGE_SIZE).subscribe({
      next: (items) => {
        this.page = 1;
        this.items.set(items);
        this.complete.set(items.length < NOTIFICATIONS_PAGE_SIZE);
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
    this.notifications.refreshCount();
  }

  loadMore(event: Event): void {
    const target = event.target as unknown as CompletableTarget;
    this.client.getMine(undefined, this.page + 1, NOTIFICATIONS_PAGE_SIZE).subscribe({
      next: (items) => {
        this.page += 1;
        this.items.update((current) => [...current, ...items.filter((item) => !current.some((c) => c.id === item.id))]);
        this.complete.set(items.length < NOTIFICATIONS_PAGE_SIZE);
        void target.complete();
      },
      error: () => void target.complete(),
    });
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
