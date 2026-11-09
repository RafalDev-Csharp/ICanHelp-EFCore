import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ConversationsClient } from '../../core/api/api-client';
import { myConversationFixture } from '../offers/testing/offer-fixtures';

import { CONVERSATIONS_PAGE_SIZE, ConversationsPage } from './conversations.page';

describe('ConversationsPage', () => {
  const client = { getMine: vi.fn() };

  function create(): { page: ConversationsPage; element: HTMLElement; render: () => Promise<void> } {
    TestBed.configureTestingModule({
      imports: [ConversationsPage],
      providers: [provideRouter([]), { provide: ConversationsClient, useValue: client }],
    });
    const fixture = TestBed.createComponent(ConversationsPage);
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

  beforeEach(() => vi.clearAllMocks());

  it('lists threads with the last offer, preview and unread count', async () => {
    client.getMine.mockReturnValue(
      of([
        myConversationFixture('c1', {
          unreadCount: 3,
          lastMessagePreview: 'Mogę jutro po 17.',
          lastOffer: {
            id: 'o1',
            price: { amount: 120, currency: 'PLN' },
            status: 'Pending',
            proposedBy: 'Helper',
            isOutdated: true,
            createdAt: '2026-11-09T10:00:00Z',
          },
        }),
        myConversationFixture('c2', { myRole: 'Helper', announcementChangedSinceLastRead: true }),
      ]),
    );
    const { element, render } = create();
    await render();

    expect(client.getMine).toHaveBeenCalledWith(undefined, undefined, undefined, 1, CONVERSATIONS_PAGE_SIZE);
    const rows = element.querySelectorAll('ion-item.conversation');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('Pomocnik: pomocnik1');
    expect(rows[0].textContent).toContain('nieaktualna');
    expect(rows[0].textContent).toContain('Mogę jutro po 17.');
    expect(rows[0].querySelector('ion-badge[color="primary"]')?.textContent).toContain('3');
    expect(rows[1].textContent).toContain('Zleceniodawca:');
    expect(rows[1].textContent).toContain('Ogłoszenie zmienione');
  });

  it('filters by role', async () => {
    client.getMine.mockReturnValue(of([]));
    const { page, element, render } = create();
    await render();
    expect(element.textContent).toContain('Brak rozmów');

    page.setRole('Helper');

    expect(client.getMine).toHaveBeenLastCalledWith('Helper', undefined, undefined, 1, CONVERSATIONS_PAGE_SIZE);
  });

  it('shows a retry state when loading fails', async () => {
    client.getMine.mockReturnValue(throwError(() => ({ status: 0 })));
    const { element, render } = create();
    await render();

    expect(element.textContent).toContain('Nie udało się wczytać rozmów');
  });
});
