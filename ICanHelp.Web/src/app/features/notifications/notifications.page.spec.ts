import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';

import type { NotificationDto } from '../../core/api/api-client';
import { NotificationsClient } from '../../core/api/api-client';
import { NotificationsService } from '../../core/notifications/notifications.service';

import { NOTIFICATIONS_PAGE_SIZE, NotificationsPage } from './notifications.page';

function notification(id: string, overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id,
    title: '',
    body: '',
    url: undefined,
    type: 'OfferReceived',
    parameters: { announcementTitle: 'Cieknący kran', price: '120.00 PLN' },
    announcementId: 'a1',
    conversationId: 'c1',
    createdAt: '2026-11-09T10:00:00Z',
    readAt: undefined,
    isRead: false,
    ...overrides,
  };
}

describe('NotificationsPage', () => {
  const client = { getMine: vi.fn() };
  const received = new Subject<NotificationDto>();
  const service = {
    unreadCount: signal(0),
    received$: received.asObservable(),
    refreshCount: vi.fn(),
    open: vi.fn(() => Promise.resolve()),
    markAllAsRead: vi.fn(() => Promise.resolve()),
  };

  function create(): { page: NotificationsPage; element: HTMLElement; render: () => Promise<void> } {
    TestBed.configureTestingModule({
      imports: [NotificationsPage],
      providers: [
        provideRouter([]),
        { provide: NotificationsClient, useValue: client },
        { provide: NotificationsService, useValue: service },
      ],
    });
    const fixture = TestBed.createComponent(NotificationsPage);
    fixture.componentInstance.ionViewWillEnter();
    fixture.detectChanges();
    return {
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: async () => {
        fixture.detectChanges();
        await fixture.whenStable();
      },
    };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    service.unreadCount.set(0);
  });

  it('lists notifications with texts and highlights unread ones', async () => {
    client.getMine.mockReturnValue(
      of([
        notification('n1'),
        notification('n2', { type: 'OfferAccepted', isRead: true, readAt: '2026-11-09T11:00:00Z' }),
      ]),
    );
    const { element, render } = create();
    await render();

    expect(client.getMine).toHaveBeenCalledWith(undefined, 1, NOTIFICATIONS_PAGE_SIZE);
    expect(service.refreshCount).toHaveBeenCalled();
    const rows = element.querySelectorAll('ion-item.notification');
    expect(rows).toHaveLength(2);
    expect(rows[0].classList).toContain('unread');
    expect(rows[0].textContent?.replace(/\s/g, ' ')).toContain('Nowa oferta: 120 zł');
    expect(rows[0].textContent).toContain('Cieknący kran');
    expect(rows[1].classList).not.toContain('unread');
    expect(rows[1].textContent).toContain('Twoja oferta została przyjęta');
    expect(element.querySelector('.mark-all')).not.toBeNull();
  });

  it('opens a notification and shows it as read', async () => {
    client.getMine.mockReturnValue(of([notification('n1')]));
    const { page, element, render } = create();
    await render();

    await page.open(page.items()[0]);
    await render();

    expect(service.open).toHaveBeenCalledWith(expect.objectContaining({ id: 'n1' }));
    expect(element.querySelector('ion-item.unread')).toBeNull();
  });

  it('marks all as read', async () => {
    client.getMine.mockReturnValue(of([notification('n1'), notification('n2')]));
    const { page, element, render } = create();
    await render();

    await page.markAll();
    await render();

    expect(service.markAllAsRead).toHaveBeenCalled();
    expect(element.querySelectorAll('ion-item.unread')).toHaveLength(0);
    expect(element.querySelector('.mark-all')).toBeNull();
  });

  it('puts a live notification on top (a renewed digest moves instead of duplicating)', async () => {
    client.getMine.mockReturnValue(
      of([notification('n1'), notification('digest', { type: 'NewAnnouncementsDigest' })]),
    );
    const { page, render } = create();
    await render();

    received.next(notification('n3', { type: 'MessageReceived' }));
    received.next(notification('digest', { type: 'NewAnnouncementsDigest', parameters: { count: '5' } }));

    expect(page.items().map((n) => n.id)).toEqual(['digest', 'n3', 'n1']);
  });

  it('shows the empty and the failed state', async () => {
    client.getMine.mockReturnValue(of([]));
    const empty = create();
    await empty.render();
    expect(empty.element.textContent).toContain('Brak powiadomień');

    TestBed.resetTestingModule();
    client.getMine.mockReturnValue(throwError(() => ({ status: 0 })));
    const failed = create();
    await failed.render();
    expect(failed.element.textContent).toContain('Nie udało się wczytać powiadomień');
  });
});
