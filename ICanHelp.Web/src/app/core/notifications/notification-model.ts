import type { NotificationDto } from '../api/api-client';

/** Powiadomienie z pusha (FCM `notification` + `data`) przekazane przez service worker do otwartej aplikacji. */
export interface PushNotice {
  notificationId?: string;
  title: string;
  body?: string;
  url?: string;
  announcementId?: string;
  conversationId?: string;
}

/** Czego dotyczy nowe powiadomienie (`NotificationsService.activity$`) – do przeładowania otwartego ekranu. */
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
