import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AnnouncementsClient, CategoriesClient, ConversationsClient } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import { notificationsStub, provideNotificationsStub } from '../../core/testing/notifications-stub';
import { announcement } from '../announcements/testing/announcement-fixtures';
import { myConversationFixture } from '../offers/testing/offer-fixtures';

import { ORDERS_PAGE_SIZE, OrdersPage } from './orders.page';

describe('OrdersPage', () => {
  const announcementsClient = { getAll: vi.fn() };
  const conversationsClient = { getMine: vi.fn() };
  const auth = { currentUser: signal({ id: 'me' }), isAuthenticated: signal(true), loadCurrentUser: vi.fn() };
  let notifications = notificationsStub();

  function create(): { page: OrdersPage; element: HTMLElement; render: () => Promise<void> } {
    notifications = notificationsStub();
    TestBed.configureTestingModule({
      imports: [OrdersPage],
      providers: [
        provideRouter([]),
        { provide: AnnouncementsClient, useValue: announcementsClient },
        { provide: ConversationsClient, useValue: conversationsClient },
        { provide: CategoriesClient, useValue: { getAll: () => of([]) } },
        { provide: AuthService, useValue: auth },
        provideNotificationsStub(notifications),
      ],
    });
    const fixture = TestBed.createComponent(OrdersPage);
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

  const groupTitles = (element: HTMLElement): string[] =>
    Array.from(element.querySelectorAll('ion-item-group')).map((group) => group.getAttribute('data-group') ?? '');

  beforeEach(() => vi.clearAllMocks());

  it('"Zlecam" lists my announcements of every status, grouped', async () => {
    announcementsClient.getAll.mockReturnValue(
      of([
        announcement('a1', { status: 'InProgress', title: 'Malowanie' }),
        announcement('a2', { status: 'Open', title: 'Kran' }),
        announcement('a3', { status: 'Cancelled', title: 'Płot' }),
      ]),
    );
    const { element, render } = create();
    await render();

    expect(announcementsClient.getAll).toHaveBeenCalledWith(
      'me',
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      1,
      ORDERS_PAGE_SIZE,
    );
    expect(groupTitles(element)).toEqual(['negotiating', 'inProgress', 'finished']);
    expect(element.querySelector('[data-group="inProgress"] app-announcement-card')?.textContent).toContain(
      'Malowanie',
    );
    expect(conversationsClient.getMine).not.toHaveBeenCalled();
  });

  it('"Pomagam" lists my helper threads grouped by the state of my offer', async () => {
    announcementsClient.getAll.mockReturnValue(of([]));
    conversationsClient.getMine.mockReturnValue(
      of([
        myConversationFixture('c1', { myRole: 'Helper', title: 'Pytanie o kran' }),
        myConversationFixture('c2', { myRole: 'Helper', announcementStatus: 'Accepted' }),
      ]),
    );
    const { page, element, render } = create();
    await render();
    expect(element.textContent).toContain('Nie masz jeszcze zleceń');

    page.setRole('Helper');
    await render();

    expect(conversationsClient.getMine).toHaveBeenCalledWith('Helper', undefined, undefined, 1, ORDERS_PAGE_SIZE);
    expect(groupTitles(element)).toEqual(['negotiating', 'finished']);
    const first = element.querySelector('[data-group="negotiating"] app-conversation-item');
    expect(first?.textContent).toContain('Pytanie o kran');
    expect(first?.textContent).toContain('Pytania i odpowiedzi');
  });

  it('reloads the active list when a notification about a thread arrives', async () => {
    announcementsClient.getAll.mockReturnValue(of([]));
    const { render } = create();
    await render();
    expect(announcementsClient.getAll).toHaveBeenCalledTimes(1);

    notifications.activity$.next({ announcementId: 'a9' });
    expect(announcementsClient.getAll).toHaveBeenCalledTimes(1);

    notifications.activity$.next({ announcementId: 'a1', conversationId: 'c1' });
    expect(announcementsClient.getAll).toHaveBeenCalledTimes(2);
  });

  it('keeps finished orders in a collapsed, greyed-out section at the bottom', async () => {
    announcementsClient.getAll.mockReturnValue(
      of([
        announcement('a1', { status: 'Completed', title: 'Płot' }),
        announcement('a2', { status: 'Accepted', title: 'Kran' }),
      ]),
    );
    const { page, element, render } = create();
    await render();

    expect(groupTitles(element)).toEqual(['accepted', 'finished']);
    const finished = element.querySelector('[data-group="finished"]');
    expect(finished?.classList).toContain('finished');
    expect(finished?.textContent).toContain('Zakończone (1)');
    expect(finished?.querySelector('app-announcement-card')).toBeNull();

    page.toggleFinished();
    await render();

    expect(element.querySelector('[data-group="finished"] app-announcement-card')?.textContent).toContain('Płot');
  });

  it('shows a retry state when loading fails', async () => {
    announcementsClient.getAll.mockReturnValue(throwError(() => ({ status: 0 })));
    const { element, render } = create();
    await render();

    expect(element.textContent).toContain('Nie udało się wczytać zleceń');
  });
});
