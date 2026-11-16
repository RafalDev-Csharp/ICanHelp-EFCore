import type { PushNotice } from '../notifications/notification-model';

/** Wiadomości z `public/app-sw.js` (te same nazwy). */
export const PUSH_RECEIVED = 'icanhelp-push-received';
export const PUSH_CLICKED = 'icanhelp-push-clicked';

/** Wiadomość service workera, którą aplikacja rozumie. */
export type PushWorkerMessage =
  /** Push przyszedł przy otwartej aplikacji – worker oddał go zamiast pokazywać systemowy dymek. */
  | { kind: 'received'; notice: PushNotice }
  /** Użytkownik kliknął systemowe powiadomienie. */
  | { kind: 'clicked'; url: string | undefined; notificationId: string | undefined };

/** Rozpoznaje wiadomość z `app-sw.js`; inne (np. Angular service workera) → `null`. */
export function parseWorkerMessage(message: unknown): PushWorkerMessage | null {
  if (!message || typeof message !== 'object') {
    return null;
  }
  const { type, title, body, data, url, notificationId } = message as {
    type?: string;
    title?: string;
    body?: string;
    url?: string;
    notificationId?: string;
    data?: Record<string, string | undefined>;
  };
  if (type === PUSH_RECEIVED && data) {
    return {
      kind: 'received',
      notice: {
        notificationId: data['notificationId'],
        title: title ?? 'ICanHelp',
        body,
        url: data['url'],
        announcementId: data['announcementId'],
        conversationId: data['conversationId'],
      },
    };
  }
  return type === PUSH_CLICKED ? { kind: 'clicked', url, notificationId } : null;
}
