import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { NotificationDto } from '../api/api-client';
import { AuthService } from '../auth/auth.service';

import type { NotificationHubConnection } from './notification-hub';
import { NOTIFICATION_HUB_FACTORY, NOTIFICATION_METHOD, RECONNECT_DELAYS_MS } from './notification-hub';

/**
 * Jedno połączenie z hubem powiadomień (SignalR): start z ponawianiem, gdy API nie odpowiada, świeży token dostępu
 * i rozłączenie. O tym, *kiedy* łączyć (zalogowanie / wylogowanie) i co zrobić z powiadomieniem, decyduje
 * `NotificationsService`.
 */
@Injectable({ providedIn: 'root' })
export class NotificationHubClient {
  private readonly auth = inject(AuthService);
  private readonly createConnection = inject(NOTIFICATION_HUB_FACTORY);

  private connection: NotificationHubConnection | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  private retryCount = 0;

  /** Połączenie istnieje (także w trakcie łączenia / ponawiania). */
  get active(): boolean {
    return this.connection !== null;
  }

  /**
   * Tworzy połączenie i zaczyna łączyć; gdy już istnieje – nic nie robi (`false`).
   *
   * @param onNotification nowe powiadomienie z serwera
   * @param onReconnected połączenie wznowione po zerwaniu – w międzyczasie coś mogło przyjść
   */
  connect(onNotification: (notification: NotificationDto) => void, onReconnected: () => void): boolean {
    if (this.connection) {
      return false;
    }
    const connection = this.createConnection(() => this.accessToken());
    connection.on(NOTIFICATION_METHOD, (notification) => onNotification(notification as NotificationDto));
    connection.onreconnected(onReconnected);
    this.connection = connection;
    this.retryCount = 0;
    this.start(connection);
    return true;
  }

  disconnect(): void {
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
    const connection = this.connection;
    this.connection = null;
    if (connection) {
      void connection.stop().catch(() => undefined);
    }
  }

  /** Pierwsze połączenie nie wznawia się samo (`withAutomaticReconnect` działa po zerwaniu) – ponawiamy sami. */
  private start(connection: NotificationHubConnection): void {
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
            this.start(connection);
          }
        }, delay);
      },
    );
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
}
