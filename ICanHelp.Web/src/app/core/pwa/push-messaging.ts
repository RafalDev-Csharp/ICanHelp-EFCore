import { InjectionToken } from '@angular/core';
import type { Messaging } from 'firebase/messaging';

import { environment } from '../../../environments/environment';
import type { PushEnvironment } from '../../../environments/push-environment';

/**
 * Cienka warstwa nad Firebase Messaging (JS SDK) – w specs podmieniana zaślepką. SDK ładujemy dopiero przy pierwszym
 * użyciu (osobny plik w buildzie), więc użytkownik bez pusha go nie pobiera.
 */
export interface PushMessaging {
  /** Konfiguracja jest uzupełniona (`environment.firebase.apiKey` i `vapidKey`). */
  readonly configured: boolean;
  /** Przeglądarka obsługuje FCM (IndexedDB, Push API, service worker). */
  isSupported(): Promise<boolean>;
  /** Token FCM dla naszej rejestracji service workera (`app-sw.js`) – Firebase nie rejestruje wtedy własnego. */
  getToken(registration: ServiceWorkerRegistration): Promise<string>;
  /** Unieważnia token (wylogowanie): FCM przestaje go przyjmować, subskrypcja push w przeglądarce znika. */
  deleteToken(registration: ServiceWorkerRegistration): Promise<void>;
}

export const PUSH_MESSAGING = new InjectionToken<PushMessaging>('PUSH_MESSAGING', {
  providedIn: 'root',
  factory: () => createFirebaseMessaging(environment),
});

export function createFirebaseMessaging(config: PushEnvironment): PushMessaging {
  let messaging: Promise<Messaging> | null = null;
  /** `deleteToken` z SDK bez wcześniejszego `getToken` w tej sesji rejestruje domyślny `firebase-messaging-sw.js`. */
  let tokenRequested = false;

  const load = (): Promise<Messaging> =>
    (messaging ??= Promise.all([import('firebase/app'), import('firebase/messaging')]).then(([app, sdk]) =>
      sdk.getMessaging(app.initializeApp(config.firebase)),
    ));

  return {
    configured: !!config.firebase.apiKey && !!config.vapidKey,
    isSupported: async () => (await import('firebase/messaging')).isSupported(),
    getToken: async (registration) => {
      const instance = await load();
      const sdk = await import('firebase/messaging');
      tokenRequested = true;
      return sdk.getToken(instance, { vapidKey: config.vapidKey, serviceWorkerRegistration: registration });
    },
    deleteToken: async (registration) => {
      if (tokenRequested) {
        const sdk = await import('firebase/messaging');
        await sdk.deleteToken(await load());
        return;
      }
      // Bez SDK: wypisanie z Push API wystarcza – FCM zgłosi token jako nieważny, a backend go usunie.
      const subscription = await registration.pushManager.getSubscription();
      await subscription?.unsubscribe();
    },
  };
}
