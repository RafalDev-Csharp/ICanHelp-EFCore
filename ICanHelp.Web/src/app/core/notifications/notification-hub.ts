import { InjectionToken } from '@angular/core';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

import { environment } from '../../../environments/environment';

/** Ścieżka huba w API (`NotificationsHub.Path`); w dev przez proxy (`/hubs` z WebSocketami). */
export const NOTIFICATIONS_HUB_PATH = '/hubs/notifications';

/** Metoda, którą wywołuje serwer (`NotificationsHub.NotificationMethod`), ciało = `NotificationDto`. */
export const NOTIFICATION_METHOD = 'notification';

/** Kolejne próby ponownego połączenia (ms), potem co 30 s – także dla pierwszego połączenia (API wyłączone). */
export const RECONNECT_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];

/** Tyle z `HubConnection`, ile używamy – w specs łatwo podmienić. */
export interface NotificationHubConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  on(method: string, handler: (...args: unknown[]) => void): void;
  onreconnected(callback: () => void): void;
}

export type NotificationHubFactory = (accessToken: () => Promise<string>) => NotificationHubConnection;

/**
 * Tworzy połączenie SignalR: token w `accessTokenFactory` (WebSocket nie wyśle nagłówka – trafia do query
 * `access_token`), automatyczne wznawianie zerwanego połączenia.
 */
export const NOTIFICATION_HUB_FACTORY = new InjectionToken<NotificationHubFactory>('NOTIFICATION_HUB_FACTORY', {
  providedIn: 'root',
  factory: () => (accessToken) =>
    new HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}${NOTIFICATIONS_HUB_PATH}`, { accessTokenFactory: accessToken })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (context) =>
          RECONNECT_DELAYS_MS[Math.min(context.previousRetryCount, RECONNECT_DELAYS_MS.length - 1)],
      })
      .configureLogging(environment.production ? LogLevel.None : LogLevel.Warning)
      .build(),
});
