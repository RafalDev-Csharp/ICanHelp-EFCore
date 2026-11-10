import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { NotificationDto } from '../api/api-client';
import { NotificationsClient } from '../api/api-client';
import { AuthService } from '../auth/auth.service';
import { ToastService } from '../ui/toast.service';

import type { NotificationHubConnection } from './notification-hub';
import { NOTIFICATION_HUB_FACTORY, NOTIFICATION_METHOD } from './notification-hub';
import { NotificationsService } from './notifications.service';

class FakeConnection implements NotificationHubConnection {
  handlers = new Map<string, (...args: unknown[]) => void>();
  reconnected: (() => void) | null = null;
  start = vi.fn(() => Promise.resolve());
  stop = vi.fn(() => Promise.resolve());

  on(method: string, handler: (...args: unknown[]) => void): void {
    this.handlers.set(method, handler);
  }

  onreconnected(callback: () => void): void {
    this.reconnected = callback;
  }

  push(notification: NotificationDto): void {
    this.handlers.get(NOTIFICATION_METHOD)?.(notification);
  }
}

function notification(overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: 'n1',
    title: '',
    body: '',
    url: undefined,
    type: 'MessageReceived',
    parameters: { announcementTitle: 'Cieknący kran', messagePreview: 'Mogę jutro.' },
    announcementId: 'a1',
    conversationId: 'c1',
    createdAt: '2026-11-09T10:00:00Z',
    readAt: undefined,
    isRead: false,
    ...overrides,
  };
}

describe('NotificationsService', () => {
  const client = {
    getUnreadCount: vi.fn(),
    markAsRead: vi.fn(),
    markAllAsRead: vi.fn(),
  };
  const toast = { action: vi.fn(() => Promise.resolve()) };
  let authenticated: ReturnType<typeof signal<boolean>>;
  let auth: {
    isAuthenticated: () => boolean;
    accessToken: () => string | null;
    isAccessTokenExpiring: ReturnType<typeof vi.fn>;
    refreshAccessToken: ReturnType<typeof vi.fn>;
  };
  let connections: FakeConnection[];
  let tokenFactories: (() => Promise<string>)[];

  function create(): NotificationsService {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: NotificationsClient, useValue: client },
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: toast },
        {
          provide: NOTIFICATION_HUB_FACTORY,
          useValue: (token: () => Promise<string>) => {
            tokenFactories.push(token);
            const connection = new FakeConnection();
            connections.push(connection);
            return connection;
          },
        },
      ],
    });
    const service = TestBed.inject(NotificationsService);
    service.start();
    TestBed.tick();
    return service;
  }

  beforeEach(() => {
    vi.clearAllMocks();
    authenticated = signal(true);
    auth = {
      isAuthenticated: () => authenticated(),
      accessToken: () => 'token-1',
      isAccessTokenExpiring: vi.fn(() => false),
      refreshAccessToken: vi.fn(() => of('token-2')),
    };
    connections = [];
    tokenFactories = [];
    client.getUnreadCount.mockReturnValue(of({ count: 2 }));
    client.markAsRead.mockReturnValue(of(undefined));
    client.markAllAsRead.mockReturnValue(of(undefined));
  });

  it('connects when signed in, loads the counter and disconnects on sign-out', () => {
    const service = create();

    expect(connections).toHaveLength(1);
    expect(connections[0].start).toHaveBeenCalled();
    expect(service.unreadCount()).toBe(2);

    authenticated.set(false);
    TestBed.tick();

    expect(connections[0].stop).toHaveBeenCalled();
    expect(service.unreadCount()).toBe(0);
  });

  it('does not connect for a guest', () => {
    authenticated.set(false);
    create();

    expect(connections).toHaveLength(0);
    expect(client.getUnreadCount).not.toHaveBeenCalled();
  });

  it('gives the hub a fresh token (refreshes an expiring one)', async () => {
    create();
    expect(await tokenFactories[0]()).toBe('token-1');

    auth.isAccessTokenExpiring.mockReturnValue(true);
    expect(await tokenFactories[0]()).toBe('token-2');
  });

  it('counts a new notification, emits it and shows a toast with a link', async () => {
    const service = create();
    const received: NotificationDto[] = [];
    service.received$.subscribe((n) => received.push(n));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    connections[0].push(notification());

    expect(service.unreadCount()).toBe(3);
    expect(received.map((n) => n.id)).toEqual(['n1']);
    expect(toast.action).toHaveBeenCalledWith(
      'Nowa wiadomość: Cieknący kran – Mogę jutro.',
      'Pokaż',
      expect.any(Function),
    );

    const onShow = (toast.action.mock.calls[0] as unknown[])[2] as () => void;
    onShow();
    await Promise.resolve();
    expect(client.markAsRead).toHaveBeenCalledWith('n1');
    expect(navigate).toHaveBeenCalledWith(['/conversations', 'c1']);
    expect(service.unreadCount()).toBe(2);
  });

  it('marks a notification from the open thread as read at once, without a toast', () => {
    const service = create();
    service.openConversation('c1');
    expect(client.markAllAsRead).toHaveBeenCalledWith('c1');
    const received: NotificationDto[] = [];
    service.received$.subscribe((n) => received.push(n));

    connections[0].push(notification({ id: 'n7' }));

    expect(client.markAsRead).toHaveBeenCalledWith('n7');
    expect(toast.action).not.toHaveBeenCalled();
    expect(received[0].isRead).toBe(true);

    service.closeConversation('c1');
    connections[0].push(notification({ id: 'n8' }));
    expect(toast.action).toHaveBeenCalledTimes(1);
  });

  it('marks all as read and restores the counter from the API on error', async () => {
    const service = create();

    await service.markAllAsRead();
    expect(service.unreadCount()).toBe(0);
    expect(client.markAllAsRead).toHaveBeenCalledWith();

    client.markAllAsRead.mockReturnValue(throwError(() => ({ status: 0 })));
    await service.markAllAsRead();
    expect(service.unreadCount()).toBe(2);
  });

  it('refreshes the counter after the hub reconnects', () => {
    const service = create();
    client.getUnreadCount.mockReturnValue(of({ count: 5 }));

    connections[0].reconnected?.();

    expect(service.unreadCount()).toBe(5);
  });

  it('retries the first connection when the API is not reachable', async () => {
    vi.useFakeTimers();
    try {
      const failing = vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValue(undefined);
      TestBed.configureTestingModule({
        providers: [
          provideRouter([]),
          { provide: NotificationsClient, useValue: client },
          { provide: AuthService, useValue: auth },
          { provide: ToastService, useValue: toast },
          {
            provide: NOTIFICATION_HUB_FACTORY,
            useValue: () => {
              const connection = new FakeConnection();
              connection.start = failing;
              return connection;
            },
          },
        ],
      });
      TestBed.inject(NotificationsService).start();
      TestBed.tick();
      expect(failing).toHaveBeenCalledTimes(1);

      await vi.advanceTimersByTimeAsync(2_000);
      expect(failing).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }
  });
});
