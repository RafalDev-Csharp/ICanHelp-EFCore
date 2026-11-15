import { InjectionToken } from '@angular/core';

/** Dostęp do API przeglądarki (Notification, service worker) – w specs zaślepka. */
export interface PushPlatform {
  /** `null` – przeglądarka bez service workera albo bez Push API / Notification. */
  readonly serviceWorker: ServiceWorkerContainer | null;
  /** Brave domyślnie blokuje push przez usługi Google (FCM) – przy błędzie pokazujemy, gdzie to włączyć. */
  readonly isBrave?: boolean;
  permission(): NotificationPermission;
  requestPermission(): Promise<NotificationPermission>;
}

/** Podpowiedź dla Brave, gdy rejestracja FCM się nie uda (ustawienie nie ma adresu, który dałoby się otworzyć linkiem). */
export const BRAVE_PUSH_HINT =
  'Brave domyślnie blokuje powiadomienia przez usługi Google. Otwórz brave://settings/privacy, włącz ' +
  '„Use Google services for push messaging” („Używaj usług Google do przesyłania wiadomości push”), uruchom ' +
  'przeglądarkę ponownie i spróbuj jeszcze raz.';

export const PUSH_PLATFORM = new InjectionToken<PushPlatform>('PUSH_PLATFORM', {
  providedIn: 'root',
  factory: () => {
    const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    return {
      serviceWorker: supported ? navigator.serviceWorker : null,
      isBrave: 'brave' in navigator,
      permission: () => (supported ? Notification.permission : 'denied'),
      requestPermission: () => Notification.requestPermission(),
    };
  },
});
