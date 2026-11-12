import { DestroyRef, Injectable, InjectionToken, computed, effect, inject, signal, untracked } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';
import { AlertController } from '@ionic/angular';
import { firstValueFrom } from 'rxjs';

import { DevicesClient } from '../api/api-client';
import { AuthService } from '../auth/auth.service';
import type { PushNotice } from '../notifications/notifications.service';
import { NotificationsService } from '../notifications/notifications.service';
import { ToastService } from '../ui/toast.service';

import { InstallPromptService } from './install-prompt.service';
import { PUSH_MESSAGING } from './push-messaging';

/**
 * `unavailable` – push wyłączony w tej wersji (brak konfiguracji Firebase albo service worker wyłączony, np. `ng serve`);
 * `needs-install` – iPhone w Safari: push działa dopiero po dodaniu do ekranu początkowego;
 * `unsupported` – przeglądarka bez Push API; `default` – można zapytać; `denied` – zablokowane w przeglądarce;
 * `enabled` – urządzenie zapisane w `api/me/devices`.
 */
export type PushState = 'unavailable' | 'needs-install' | 'unsupported' | 'default' | 'denied' | 'enabled';

/** Wiadomości z `public/app-sw.js` (te same nazwy). */
export const PUSH_RECEIVED = 'icanhelp-push-received';
export const PUSH_CLICKED = 'icanhelp-push-clicked';

/** Klucz w `localStorage`: zapisane urządzenie (do `DELETE api/me/devices/{id}`) i kiedy ostatnio pytaliśmy o zgodę. */
export const PUSH_STORAGE_KEY = 'icanhelp.push';
/** Zachęty („Powiadomić Cię…?”) nie powtarzamy częściej niż raz na tydzień. */
export const SUGGEST_INTERVAL_MS = 7 * 24 * 60 * 60 * 1000;
const READY_TIMEOUT_MS = 40_000;

/** Dostęp do API przeglądarki (Notification, service worker) – w specs zaślepka. */
export interface PushPlatform {
  /** `null` – przeglądarka bez service workera albo bez Push API / Notification. */
  readonly serviceWorker: ServiceWorkerContainer | null;
  permission(): NotificationPermission;
  requestPermission(): Promise<NotificationPermission>;
}

export const PUSH_PLATFORM = new InjectionToken<PushPlatform>('PUSH_PLATFORM', {
  providedIn: 'root',
  factory: () => {
    const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    return {
      serviceWorker: supported ? navigator.serviceWorker : null,
      permission: () => (supported ? Notification.permission : 'denied'),
      requestPermission: () => Notification.requestPermission(),
    };
  },
});

interface StoredPush {
  deviceId?: string;
  token?: string;
  suggestedAt?: number;
}

/**
 * Powiadomienia push (F7, FCM). Zgoda przeglądarki tylko po akcji użytkownika: `suggest()` po złożeniu oferty /
 * dodaniu ogłoszenia albo przycisk „Włącz” w profilu. Token z `getToken` (rejestracja `app-sw.js`) → `POST api/me/devices`;
 * przy każdym starcie zalogowanej aplikacji ze zgodą – ponownie (token mógł się zmienić, backend robi upsert).
 * Wylogowanie: `unregister()` (DELETE w API, póki jest sesja) + unieważnienie tokenu; utrata sesji bez wylogowania –
 * samo unieważnienie. Push przy otwartej aplikacji service worker oddaje tu → toast z `NotificationsService`.
 */
@Injectable({ providedIn: 'root' })
export class PushService {
  private readonly auth = inject(AuthService);
  private readonly devices = inject(DevicesClient);
  private readonly notifications = inject(NotificationsService);
  private readonly alerts = inject(AlertController);
  private readonly toast = inject(ToastService);
  private readonly installPrompt = inject(InstallPromptService);
  private readonly messaging = inject(PUSH_MESSAGING);
  private readonly platform = inject(PUSH_PLATFORM);
  private readonly serviceWorkerEnabled = inject(SwUpdate, { optional: true })?.isEnabled ?? false;

  private readonly started = signal(false);
  private readonly permission = signal<NotificationPermission>(this.platform.permission());
  private readonly registered = signal(false);
  private readonly firebaseSupported = signal(true);

  /** W trakcie włączania (przycisk w profilu pokazuje spinner). */
  readonly busy = signal(false);

  readonly state = computed<PushState>(() => {
    if (!this.messaging.configured || !this.serviceWorkerEnabled) {
      return 'unavailable';
    }
    if (this.installPrompt.isIos && !this.installPrompt.isStandalone()) {
      return 'needs-install';
    }
    if (!this.platform.serviceWorker || !this.firebaseSupported()) {
      return 'unsupported';
    }
    const permission = this.permission();
    if (permission === 'denied') {
      return 'denied';
    }
    return permission === 'granted' && this.registered() ? 'enabled' : 'default';
  });

  constructor() {
    effect(() => {
      if (!this.started()) {
        return;
      }
      const authenticated = this.auth.isAuthenticated();
      untracked(() => void (authenticated ? this.sync() : this.forget()));
    });

    const container = this.platform.serviceWorker;
    if (container) {
      const onMessage = (event: MessageEvent): void => this.onWorkerMessage(event.data);
      container.addEventListener('message', onMessage);
      inject(DestroyRef).onDestroy(() => container.removeEventListener('message', onMessage));
    }
  }

  /** Raz, przy starcie zakładek. Dalej sterują logowanie i wylogowanie. */
  start(): void {
    if (this.started() || this.state() === 'unavailable') {
      return;
    }
    this.started.set(true);
    if (this.platform.serviceWorker) {
      this.messaging.isSupported().then(
        (supported) => this.firebaseSupported.set(supported),
        () => this.firebaseSupported.set(false),
      );
    }
  }

  /**
   * Zachęta po akcji użytkownika (np. „Powiadomić Cię o odpowiedzi?”) – tylko gdy zgody jeszcze nie było, najwyżej raz
   * na tydzień. „Włącz” od razu prosi przeglądarkę o zgodę (wymaga kliknięcia, dlatego okno, a nie automat).
   */
  async suggest(message: string, now: number = Date.now()): Promise<void> {
    if (this.state() !== 'default' || this.permission() === 'granted' || !this.auth.isAuthenticated()) {
      return;
    }
    const stored = readStored();
    if (stored.suggestedAt && now - stored.suggestedAt < SUGGEST_INTERVAL_MS) {
      return;
    }
    writeStored({ ...stored, suggestedAt: now });

    const alert = await this.alerts.create({
      header: 'Powiadomienia',
      message,
      buttons: [
        { text: 'Nie teraz', role: 'cancel' },
        { text: 'Włącz', role: 'confirm', handler: () => void this.enable() },
      ],
    });
    await alert.present();
  }

  /** Zgoda przeglądarki + zapis urządzenia. Wołać bezpośrednio z obsługi kliknięcia. */
  async enable(): Promise<boolean> {
    // Pierwsza instrukcja – synchronicznie w obsłudze kliknięcia (Safari wymaga gestu użytkownika).
    const permission = await this.platform.requestPermission();
    this.permission.set(permission);
    if (permission !== 'granted') {
      if (permission === 'denied') {
        void this.toast.info('Powiadomienia są zablokowane – możesz je włączyć w ustawieniach przeglądarki.');
      }
      return false;
    }

    this.busy.set(true);
    try {
      const ok = await this.register();
      if (ok) {
        void this.toast.success('Powiadomienia włączone.');
      } else {
        void this.toast.error('Nie udało się włączyć powiadomień. Spróbuj ponownie później.');
      }
      return ok;
    } finally {
      this.busy.set(false);
    }
  }

  /** Przed wylogowaniem (póki jest token dostępu): usuwa urządzenie w API i unieważnia token FCM. */
  async unregister(): Promise<void> {
    const deviceId = readStored().deviceId;
    if (deviceId && this.auth.isAuthenticated()) {
      await firstValueFrom(this.devices.remove(deviceId), { defaultValue: undefined }).catch(() => undefined);
    }
    await this.forget();
  }

  /** Zalogowany ze zgodą: (ponowny) zapis tokenu. Bez zgody nic nie robimy – pytamy dopiero po akcji użytkownika. */
  private async sync(): Promise<void> {
    this.permission.set(this.platform.permission());
    if (this.state() !== 'default' || this.permission() !== 'granted') {
      return;
    }
    await this.register();
  }

  private async register(): Promise<boolean> {
    try {
      const registration = await this.registration();
      if (!registration || !this.auth.isAuthenticated()) {
        return false;
      }
      const token = await this.messaging.getToken(registration);
      const device = await firstValueFrom(this.devices.register({ token, platform: 'Web' }));
      writeStored({ ...readStored(), deviceId: device.id, token });
      this.registered.set(true);
      return true;
    } catch {
      return false;
    }
  }

  /** Sesja się skończyła: token FCM unieważniony lokalnie (backend usunie go przy następnej wysyłce). */
  private async forget(): Promise<void> {
    const stored = readStored();
    this.registered.set(false);
    if (!stored.token) {
      return;
    }
    writeStored({ suggestedAt: stored.suggestedAt });
    const registration = await this.platform.serviceWorker?.getRegistration().catch(() => undefined);
    if (registration) {
      await this.messaging.deleteToken(registration).catch(() => undefined);
    }
  }

  /** Rejestracja `app-sw.js` (`registerWhenStable` może ją opóźnić o kilkadziesiąt sekund po starcie). */
  private registration(): Promise<ServiceWorkerRegistration | null> {
    const container = this.platform.serviceWorker;
    if (!container) {
      return Promise.resolve(null);
    }
    return Promise.race([
      container.ready,
      new Promise<null>((resolve) => setTimeout(() => resolve(null), READY_TIMEOUT_MS)),
    ]);
  }

  private onWorkerMessage(message: unknown): void {
    if (!message || typeof message !== 'object') {
      return;
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
      const notice: PushNotice = {
        notificationId: data['notificationId'],
        title: title ?? 'ICanHelp',
        body,
        url: data['url'],
        conversationId: data['conversationId'],
      };
      this.notifications.showPush(notice);
    } else if (type === PUSH_CLICKED) {
      void this.notifications.openPush(url, notificationId);
    }
  }
}

function readStored(): StoredPush {
  try {
    const value = JSON.parse(localStorage.getItem(PUSH_STORAGE_KEY) ?? '{}') as StoredPush | null;
    return value && typeof value === 'object' ? value : {};
  } catch {
    return {};
  }
}

function writeStored(value: StoredPush): void {
  try {
    localStorage.setItem(PUSH_STORAGE_KEY, JSON.stringify(value));
  } catch {
    // tryb prywatny / pełny magazyn – push i tak zadziała do końca tej sesji
  }
}
