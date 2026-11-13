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

/** Ile ostatnich id powiadomień pamiętamy do odsiewania duplikatów (hub + push). */
const SEEN_LIMIT = 200;

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
  private readonly activitySubject = new Subject<NotificationActivity>();
  private connection: NotificationHubConnection | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private retryCount = 0;
  /** Wątek otwarty na ekranie (`ConversationPage`). */
  private activeConversationId: string | null = null;
  /**
   * Ostatnio pokazane powiadomienia (id). To samo może przyjść dwiema drogami – hubem i pushem przy otwartej
   * aplikacji (F7); toast pokazujemy raz, a licznik zwiększamy tylko za pierwszym razem.
   */
  private readonly seen = new Set<string>();

  /** Liczba nieprzeczytanych (plakietka zakładki „Powiadomienia”). */
  readonly unreadCount = signal(0);
  /** Nowe powiadomienia z huba (już po aktualizacji licznika). */
  readonly received$: Observable<NotificationDto> = this.receivedSubject.asObservable();
  /**
   * F11 D: czego dotyczy nowe powiadomienie – z huba **i** z pusha przy otwartej aplikacji (gdy hub nie działa albo
   * push był pierwszy). Otwarte ekrany (wątek, szczegóły ogłoszenia) przeładowują dane, jeśli to ich ogłoszenie / wątek.
   * To samo zdarzenie może przyjść dwa razy (hub + push) – przeładowanie jest bezpieczne.
   */
  readonly activity$: Observable<NotificationActivity> = this.activitySubject.asObservable();

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

  /**
   * Push przyszedł przy otwartej aplikacji (service worker nie pokazał systemowego dymka): toast jak z huba, chyba że
   * hub już to powiadomienie dostarczył albo użytkownik patrzy na ten wątek.
   */
  showPush(push: PushNotice): void {
    if (!push.notificationId || !this.remember(push.notificationId) || !this.auth.isAuthenticated()) {
      return;
    }
    this.activitySubject.next({ announcementId: push.announcementId, conversationId: push.conversationId });
    if (push.conversationId && push.conversationId === this.activeConversationId) {
      return;
    }
    this.refreshCount();
    if (!this.router.url.startsWith('/notifications')) {
      const message = push.body ? `${push.title} – ${push.body}` : push.title;
      void this.toast.action(message, 'Pokaż', () => void this.openPush(push.url, push.notificationId));
    }
  }

  /** Kliknięcie w push (albo toast z pusha): przeczytane + ekran z `data.url`. */
  async openPush(url: string | undefined, notificationId?: string): Promise<void> {
    if (notificationId && this.auth.isAuthenticated()) {
      this.client.markAsRead(notificationId).subscribe({ next: () => this.refreshCount(), error: () => undefined });
    }
    await this.router.navigateByUrl(url && url.startsWith('/') && !url.startsWith('//') ? url : '/notifications');
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
    this.activitySubject.next({
      announcementId: notification.announcementId,
      conversationId: notification.conversationId,
    });
    if (notification.conversationId && notification.conversationId === this.activeConversationId) {
      // Użytkownik patrzy na ten wątek – widzi zmianę od razu (ekran przeładuje wątek).
      this.client.markAsRead(notification.id).subscribe({ error: () => undefined });
      this.receivedSubject.next(asRead(notification));
      return;
    }

    const firstTime = this.remember(notification.id);
    if (firstTime) {
      this.unreadCount.update((count) => count + 1);
    } else {
      this.refreshCount(); // push był pierwszy (i już odświeżył licznik) – tu tylko pewność, bez podwójnego +1
    }
    this.receivedSubject.next(notification);

    // Na liście powiadomień nowe pojawia się na górze – toast byłby powtórzeniem.
    if (firstTime && !this.router.url.startsWith('/notifications')) {
      const text = describeNotification(notification);
      const message = text.body ? `${text.title} – ${text.body}` : text.title;
      void this.toast.action(message, 'Pokaż', () => void this.open(notification));
    }
  }

  /** `true`, gdy to powiadomienie widzimy pierwszy raz. */
  private remember(notificationId: string): boolean {
    if (this.seen.has(notificationId)) {
      return false;
    }
    this.seen.add(notificationId);
    if (this.seen.size > SEEN_LIMIT) {
      this.seen.delete(this.seen.values().next().value as string);
    }
    return true;
  }
}

/** Powiadomienie z pusha (FCM `notification` + `data`) przekazane przez service worker do otwartej aplikacji. */
export interface PushNotice {
  notificationId?: string;
  title: string;
  body?: string;
  url?: string;
  announcementId?: string;
  conversationId?: string;
}

/** Czego dotyczy nowe powiadomienie (`activity$`) – do przeładowania otwartego ekranu. */
export interface NotificationActivity {
  announcementId?: string;
  conversationId?: string;
}

export function isRead(notification: NotificationDto): boolean {
  return notification.isRead || !!notification.readAt;
}

/** Kopia oznaczona jako przeczytana (lista po kliknięciu / „Oznacz wszystkie”). */
export function asRead(notification: NotificationDto): NotificationDto {
  return { ...notification, isRead: true, readAt: notification.readAt ?? new Date().toISOString() };
}
