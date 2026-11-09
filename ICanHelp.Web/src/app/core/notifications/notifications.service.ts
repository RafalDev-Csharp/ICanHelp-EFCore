import { DestroyRef, Injectable, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import type { Observable } from 'rxjs';
import { Subject, firstValueFrom } from 'rxjs';

import type { NotificationDto } from '../api/api-client';
import { NotificationsClient } from '../api/api-client';
import { AuthService } from '../auth/auth.service';
import { ToastService } from '../ui/toast.service';

import type { NotificationHubConnection } from './notification-hub';
import { NOTIFICATION_HUB_FACTORY, NOTIFICATION_METHOD, RECONNECT_DELAYS_MS } from './notification-hub';
import { describeNotification, notificationLink } from './notification-texts';

/**
 * Powiadomienia w aplikacji (F6): połączenie z hubem SignalR, dopóki użytkownik jest zalogowany, licznik
 * nieprzeczytanych (plakietka na zakładce), toast przy nowym i strumień `received$` dla otwartych ekranów
 * (lista powiadomień, wątek, lista rozmów). Hub tylko przyspiesza – stan z API (`unread-count`) wczytujemy przy
 * zalogowaniu, po wznowieniu połączenia i po powrocie do karty.
 * Nowe powiadomienie z wątku, który jest właśnie otwarty, od razu oznaczamy jako przeczytane (bez toastu).
 */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly client = inject(NotificationsClient);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly createConnection = inject(NOTIFICATION_HUB_FACTORY);

  private readonly started = signal(false);
  private readonly receivedSubject = new Subject<NotificationDto>();
  private connection: NotificationHubConnection | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private retryCount = 0;
  /** Wątek otwarty na ekranie (`ConversationPage`). */
  private activeConversationId: string | null = null;

  /** Liczba nieprzeczytanych (plakietka zakładki „Powiadomienia”). */
  readonly unreadCount = signal(0);
  /** Nowe powiadomienia z huba (już po aktualizacji licznika). */
  readonly received$: Observable<NotificationDto> = this.receivedSubject.asObservable();

  constructor() {
    // Zalogowanie → połączenie i licznik; wylogowanie (także w innej karcie) → rozłączenie i zero.
    effect(() => {
      const active = this.started() && this.auth.isAuthenticated();
      untracked(() => (active ? this.connect() : this.disconnect()));
    });

    const onVisible = (): void => {
      if (document.visibilityState === 'visible' && this.connection) {
        this.refreshCount();
      }
    };
    document.addEventListener('visibilitychange', onVisible);
    inject(DestroyRef).onDestroy(() => {
      document.removeEventListener('visibilitychange', onVisible);
      this.disconnect();
    });
  }

  /** Włącza powiadomienia (raz, przy starcie zakładek); dalej sterują nimi logowanie i wylogowanie. */
  start(): void {
    this.started.set(true);
  }

  refreshCount(): void {
    if (!this.auth.isAuthenticated()) {
      return;
    }
    this.client.getUnreadCount().subscribe({
      next: (result) => this.unreadCount.set(result.count),
      error: () => undefined,
    });
  }

  /** Jedno przeczytane (np. kliknięcie na liście). Licznik zmniejszamy od razu. */
  markAsRead(notification: NotificationDto): void {
    if (isRead(notification)) {
      return;
    }
    this.unreadCount.update((count) => Math.max(0, count - 1));
    this.client.markAsRead(notification.id).subscribe({ error: () => this.refreshCount() });
  }

  /** Wszystkie przeczytane („Oznacz wszystkie”). */
  markAllAsRead(): Promise<void> {
    this.unreadCount.set(0);
    return firstValueFrom(this.client.markAllAsRead(), { defaultValue: undefined }).catch(() => this.refreshCount());
  }

  /**
   * Ekran wątku się otworzył: jego powiadomienia (wiadomości, oferty) są widziane – `read-all?conversationId=`,
   * a nowe z tego wątku nie pokażą toastu.
   */
  openConversation(conversationId: string): void {
    this.activeConversationId = conversationId;
    if (!this.auth.isAuthenticated()) {
      return;
    }
    this.client.markAllAsRead(conversationId).subscribe({
      next: () => this.refreshCount(),
      error: () => undefined,
    });
  }

  closeConversation(conversationId: string): void {
    if (this.activeConversationId === conversationId) {
      this.activeConversationId = null;
    }
  }

  /** Kliknięcie w powiadomienie (lista, toast): przeczytane + właściwy ekran. */
  async open(notification: NotificationDto): Promise<void> {
    this.markAsRead(notification);
    const link = notificationLink(notification);
    if (link) {
      await this.router.navigate(link);
    }
  }

  private connect(): void {
    if (this.connection) {
      return;
    }
    const connection = this.createConnection(() => this.accessToken());
    connection.on(NOTIFICATION_METHOD, (notification) => this.onNotification(notification as NotificationDto));
    connection.onreconnected(() => this.refreshCount());
    this.connection = connection;
    this.retryCount = 0;
    this.refreshCount();
    this.startConnection(connection);
  }

  /** Pierwsze połączenie nie wznawia się samo (`withAutomaticReconnect` działa po zerwaniu) – ponawiamy sami. */
  private startConnection(connection: NotificationHubConnection): void {
    connection.start().then(
      () => (this.retryCount = 0),
      () => {
        if (this.connection !== connection) {
          return;
        }
        const delay = RECONNECT_DELAYS_MS[Math.min(this.retryCount + 1, RECONNECT_DELAYS_MS.length - 1)];
        this.retryCount += 1;
        this.retryTimer = setTimeout(() => {
          this.retryTimer = null;
          if (this.connection === connection) {
            this.startConnection(connection);
          }
        }, delay);
      },
    );
  }

  private disconnect(): void {
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
    const connection = this.connection;
    this.connection = null;
    this.unreadCount.set(0);
    if (connection) {
      void connection.stop().catch(() => undefined);
    }
  }

  /** Token do huba – przed wygaśnięciem odświeżamy (jak `authInterceptor`). */
  private async accessToken(): Promise<string> {
    if (this.auth.isAccessTokenExpiring()) {
      try {
        return await firstValueFrom(this.auth.refreshAccessToken());
      } catch {
        return '';
      }
    }
    return this.auth.accessToken() ?? '';
  }

  private onNotification(notification: NotificationDto): void {
    if (notification.conversationId && notification.conversationId === this.activeConversationId) {
      // Użytkownik patrzy na ten wątek – widzi zmianę od razu (ekran przeładuje wątek).
      this.client.markAsRead(notification.id).subscribe({ error: () => undefined });
      this.receivedSubject.next(asRead(notification));
      return;
    }

    this.unreadCount.update((count) => count + 1);
    this.receivedSubject.next(notification);

    // Na liście powiadomień nowe pojawia się na górze – toast byłby powtórzeniem.
    if (!this.router.url.startsWith('/notifications')) {
      const text = describeNotification(notification);
      const message = text.body ? `${text.title} – ${text.body}` : text.title;
      void this.toast.action(message, 'Pokaż', () => void this.open(notification));
    }
  }
}

export function isRead(notification: NotificationDto): boolean {
  return notification.isRead || !!notification.readAt;
}

/** Kopia oznaczona jako przeczytana (lista po kliknięciu / „Oznacz wszystkie”). */
export function asRead(notification: NotificationDto): NotificationDto {
  return { ...notification, isRead: true, readAt: notification.readAt ?? new Date().toISOString() };
}
