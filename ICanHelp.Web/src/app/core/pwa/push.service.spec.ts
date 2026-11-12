import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SwUpdate } from '@angular/service-worker';
import { AlertController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import { DevicesClient } from '../api/api-client';
import { AuthService } from '../auth/auth.service';
import { NotificationsService } from '../notifications/notifications.service';
import { ToastService } from '../ui/toast.service';

import { InstallPromptService } from './install-prompt.service';
import type { PushMessaging } from './push-messaging';
import { PUSH_MESSAGING } from './push-messaging';
import type { PushPlatform } from './push.service';
import { PUSH_CLICKED, PUSH_PLATFORM, PUSH_RECEIVED, PUSH_STORAGE_KEY, PushService } from './push.service';

/** Fałszywy `navigator.serviceWorker`: gotowa rejestracja i wiadomości „z workera”. */
class FakeContainer extends EventTarget {
  readonly registration = { scope: '/' } as ServiceWorkerRegistration;
  readonly ready = Promise.resolve(this.registration);
  getRegistration = vi.fn(() => Promise.resolve(this.registration));

  send(data: unknown): void {
    this.dispatchEvent(new MessageEvent('message', { data }));
  }
}

describe('PushService', () => {
  const devices = { register: vi.fn(), remove: vi.fn() };
  const notifications = { showPush: vi.fn(), openPush: vi.fn(() => Promise.resolve()) };
  const toast = { success: vi.fn(), error: vi.fn(), info: vi.fn() };
  let alertButtons: { text: string; role?: string; handler?: () => void }[];
  const alerts = {
    create: vi.fn((options: { buttons: typeof alertButtons }) => {
      alertButtons = options.buttons;
      return Promise.resolve({ present: () => Promise.resolve() });
    }),
  };
  let messaging: Omit<PushMessaging, 'configured'> & {
    configured: boolean;
    getToken: ReturnType<typeof vi.fn>;
    deleteToken: ReturnType<typeof vi.fn>;
  };
  let container: FakeContainer;
  let permission: NotificationPermission;
  let platform: PushPlatform & { requestPermission: ReturnType<typeof vi.fn> };
  let authenticated: ReturnType<typeof signal<boolean>>;
  let install: { isIos: boolean; isStandalone: () => boolean };
  let swEnabled: boolean;

  function create(): PushService {
    TestBed.configureTestingModule({
      providers: [
        { provide: DevicesClient, useValue: devices },
        { provide: NotificationsService, useValue: notifications },
        { provide: ToastService, useValue: toast },
        { provide: AlertController, useValue: alerts },
        { provide: AuthService, useValue: { isAuthenticated: () => authenticated() } },
        { provide: InstallPromptService, useValue: install },
        { provide: SwUpdate, useValue: { isEnabled: swEnabled } },
        { provide: PUSH_MESSAGING, useValue: messaging },
        { provide: PUSH_PLATFORM, useValue: platform },
      ],
    });
    const service = TestBed.inject(PushService);
    service.start();
    TestBed.tick();
    return service;
  }

  const stored = (): Record<string, unknown> => JSON.parse(localStorage.getItem(PUSH_STORAGE_KEY) ?? '{}');

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    messaging = {
      configured: true,
      isSupported: () => Promise.resolve(true),
      getToken: vi.fn(() => Promise.resolve('fcm-token')),
      deleteToken: vi.fn(() => Promise.resolve()),
    };
    container = new FakeContainer();
    permission = 'default';
    platform = {
      serviceWorker: container as unknown as ServiceWorkerContainer,
      permission: () => permission,
      requestPermission: vi.fn((): Promise<NotificationPermission> => Promise.resolve((permission = 'granted'))),
    };
    authenticated = signal(true);
    install = { isIos: false, isStandalone: () => false };
    swEnabled = true;
    devices.register.mockReturnValue(of({ id: 'dev-1', platform: 'Web' }));
    devices.remove.mockReturnValue(of(undefined));
  });

  it('asks the browser only after a click, then registers the FCM token as a web device', async () => {
    const service = create();
    expect(platform.requestPermission).not.toHaveBeenCalled();
    expect(service.state()).toBe('default');

    await expect(service.enable()).resolves.toBe(true);

    expect(messaging.getToken).toHaveBeenCalledWith(container.registration);
    expect(devices.register).toHaveBeenCalledWith({ token: 'fcm-token', platform: 'Web' });
    expect(stored()).toMatchObject({ deviceId: 'dev-1', token: 'fcm-token' });
    expect(service.state()).toBe('enabled');
    expect(toast.success).toHaveBeenCalledWith('Powiadomienia włączone.');
  });

  it('registers again on start when the permission is already granted', async () => {
    permission = 'granted';
    const service = create();

    await vi.waitFor(() => expect(devices.register).toHaveBeenCalledTimes(1));
    expect(platform.requestPermission).not.toHaveBeenCalled();
    await vi.waitFor(() => expect(service.state()).toBe('enabled'));
  });

  it('does nothing without a permission and reports a refusal', async () => {
    platform.requestPermission.mockImplementation((): Promise<NotificationPermission> =>
      Promise.resolve((permission = 'denied')),
    );
    const service = create();

    await expect(service.enable()).resolves.toBe(false);

    expect(devices.register).not.toHaveBeenCalled();
    expect(service.state()).toBe('denied');
    expect(toast.info).toHaveBeenCalled();
  });

  it('tells the user when the registration fails', async () => {
    devices.register.mockReturnValue(throwError(() => new Error('offline')));
    const service = create();

    await expect(service.enable()).resolves.toBe(false);

    expect(toast.error).toHaveBeenCalled();
    expect(service.state()).toBe('default');
  });

  it('suggests push once a week and enables it from the alert button', async () => {
    const service = create();
    const now = Date.UTC(2026, 10, 10);

    await service.suggest('Powiadomić Cię o odpowiedzi?', now);
    expect(alerts.create).toHaveBeenCalledWith(expect.objectContaining({ message: 'Powiadomić Cię o odpowiedzi?' }));

    await service.suggest('Znowu?', now + 60_000);
    expect(alerts.create).toHaveBeenCalledTimes(1);

    alertButtons.find((button) => button.text === 'Włącz')?.handler?.();
    await vi.waitFor(() => expect(devices.register).toHaveBeenCalled());
  });

  it('does not suggest when push is already decided, unavailable or the user is signed out', async () => {
    permission = 'granted';
    await create().suggest('?');
    TestBed.resetTestingModule();

    permission = 'default';
    messaging.configured = false;
    const unconfigured = create();
    expect(unconfigured.state()).toBe('unavailable');
    await unconfigured.suggest('?');
    TestBed.resetTestingModule();

    messaging.configured = true;
    authenticated.set(false);
    await create().suggest('?');

    expect(alerts.create).not.toHaveBeenCalled();
  });

  it('is unavailable without the service worker (ng serve) and needs installation on an iPhone', () => {
    swEnabled = false;
    expect(create().state()).toBe('unavailable');
    TestBed.resetTestingModule();

    swEnabled = true;
    install = { isIos: true, isStandalone: () => false };
    expect(create().state()).toBe('needs-install');
    TestBed.resetTestingModule();

    install = { isIos: false, isStandalone: () => false };
    platform = { ...platform, serviceWorker: null };
    expect(create().state()).toBe('unsupported');
  });

  it('removes the device in the API and invalidates the token before sign-out', async () => {
    const service = create();
    await service.enable();

    await service.unregister();

    expect(devices.remove).toHaveBeenCalledWith('dev-1');
    expect(messaging.deleteToken).toHaveBeenCalledWith(container.registration);
    expect(stored()['deviceId']).toBeUndefined();
    expect(service.state()).toBe('default');
  });

  it('invalidates the token locally when the session ends without signing out', async () => {
    const service = create();
    await service.enable();

    authenticated.set(false);
    TestBed.tick();

    await vi.waitFor(() => expect(messaging.deleteToken).toHaveBeenCalled());
    expect(devices.remove).not.toHaveBeenCalled();
    expect(service.state()).toBe('default');
  });

  it('passes pushes from the service worker to the in-app toast and opens clicked links', () => {
    create();

    container.send({
      type: PUSH_RECEIVED,
      title: 'Nowa oferta',
      body: 'Jan: 90 zł',
      data: { notificationId: 'n1', url: '/conversations/c1', conversationId: 'c1' },
    });
    container.send({ type: PUSH_CLICKED, url: '/announcements/a1', notificationId: 'n2' });
    container.send('something else');

    expect(notifications.showPush).toHaveBeenCalledWith({
      notificationId: 'n1',
      title: 'Nowa oferta',
      body: 'Jan: 90 zł',
      url: '/conversations/c1',
      conversationId: 'c1',
    });
    expect(notifications.openPush).toHaveBeenCalledWith('/announcements/a1', 'n2');
  });
});
