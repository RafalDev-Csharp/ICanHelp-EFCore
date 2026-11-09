import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { ConversationDto, ConversationEntryDto, UserDto } from '../../core/api/api-client';
import { ConversationsClient, UsersClient } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import { OffersService } from '../offers/offers.service';
import { conversationFixture, offerFixture } from '../offers/testing/offer-fixtures';

import { ConversationPage } from './conversation.page';

describe('ConversationPage', () => {
  const conversations = { getById: vi.fn(), postMessage: vi.fn(), markAsRead: vi.fn() };
  const users = { getById: vi.fn() };
  const offers = {
    make: vi.fn(),
    accept: vi.fn(() => Promise.resolve(true)),
    reject: vi.fn(),
    counter: vi.fn(() => Promise.resolve(true)),
    withdraw: vi.fn(),
  };
  const auth = {
    currentUser: signal<UserDto | null>({ id: 'owner-1' } as UserDto),
    loadCurrentUser: vi.fn(() => of()),
  };

  const messageEntry: ConversationEntryDto = {
    type: 'Message',
    at: '2026-11-09T10:00:00Z',
    message: {
      id: 'm1',
      senderId: 'helper-1',
      senderRole: 'Helper',
      text: 'Mogę jutro.',
      sentAt: '2026-11-09T10:00:00Z',
      announcementRevision: 0,
    },
  } as ConversationEntryDto;
  const offerEntry = (overrides = {}): ConversationEntryDto =>
    ({ type: 'Offer', at: '2026-11-09T11:00:00Z', offer: offerFixture('o1', overrides) }) as ConversationEntryDto;

  function create(conversation: ConversationDto): {
    page: ConversationPage;
    element: HTMLElement;
    render: () => Promise<void>;
  } {
    conversations.getById.mockReturnValue(of(conversation));
    TestBed.configureTestingModule({
      imports: [ConversationPage],
      providers: [
        provideRouter([]),
        { provide: ConversationsClient, useValue: conversations },
        { provide: UsersClient, useValue: users },
        { provide: OffersService, useValue: offers },
        { provide: AuthService, useValue: auth },
      ],
    });
    const fixture = TestBed.createComponent(ConversationPage);
    fixture.componentRef.setInput('conversationId', 'c1');
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
    users.getById.mockReturnValue(of({ id: 'helper-1', fullName: 'Anna Nowak' }));
    conversations.markAsRead.mockReturnValue(of(undefined));
  });

  it('shows the thread, the other party and marks it as read', async () => {
    const { element, render } = create(
      conversationFixture({ myRole: 'Client', unreadCount: 1, timeline: [messageEntry, offerEntry()] }),
    );
    await render();

    expect(element.querySelector('ion-title')?.textContent).toContain('Anna Nowak');
    expect(element.querySelector('.bubble')?.textContent).toContain('Mogę jutro.');
    expect(element.querySelector('.message.mine')).toBeNull();
    expect(element.querySelector('.offer-price')?.textContent?.replace(/\s/g, ' ')).toContain('120 zł');
    expect(conversations.markAsRead).toHaveBeenCalledWith('c1');

    const buttons = [...element.querySelectorAll('.offer-actions ion-button')].map((b) => b.textContent?.trim());
    expect(buttons).toEqual(['Odrzuć', 'Kontroferta', 'Przyjmij']);
  });

  it('offers only a counter-offer for an outdated offer', async () => {
    const { element, render } = create(
      conversationFixture({ myRole: 'Client', timeline: [offerEntry({ isOutdated: true })] }),
    );
    await render();

    expect(element.querySelector('.offer-outdated')?.textContent).toContain('Nieaktualna');
    const buttons = [...element.querySelectorAll('.offer-actions ion-button')].map((b) => b.textContent?.trim());
    expect(buttons).toEqual(['Odrzuć', 'Zaproponuj kontrofertę']);
    expect(conversations.markAsRead).not.toHaveBeenCalled();
  });

  it('accepts through OffersService and reloads the thread', async () => {
    const { page, render } = create(conversationFixture({ myRole: 'Client', timeline: [offerEntry()] }));
    await render();

    await page.accept(offerFixture('o1'));

    expect(offers.accept).toHaveBeenCalled();
    expect(conversations.getById).toHaveBeenCalledTimes(2);
  });

  it('sends a message and clears the field', async () => {
    conversations.postMessage.mockReturnValue(of({ id: 'm2' }));
    const { page, render } = create(conversationFixture());
    await render();

    page.draft = '  Czy jest zawór?  ';
    page.send();

    expect(conversations.postMessage).toHaveBeenCalledWith('c1', { text: 'Czy jest zawór?' });
    expect(page.draft).toBe('');
  });

  it('lets the helper make a new offer when none is pending', async () => {
    const { page, element, render } = create(conversationFixture({ timeline: [offerEntry({ status: 'Rejected' })] }));
    await render();

    expect(element.querySelector('ion-footer')?.textContent).toContain('Złóż ofertę');
    expect(page.canMakeOffer()).toBe(true);
  });

  it('is read-only when the conversation is closed', async () => {
    const { element, render } = create(conversationFixture({ announcementStatus: 'Completed', canWrite: false }));
    await render();

    expect(element.querySelector('ion-textarea')).toBeNull();
    expect(element.querySelector('ion-footer')?.textContent).toContain('tylko do odczytu');
  });

  it('shows a not-found state for a foreign thread', async () => {
    conversations.getById.mockReturnValue(throwError(() => ({ status: 404 })));
    TestBed.configureTestingModule({
      imports: [ConversationPage],
      providers: [
        provideRouter([]),
        { provide: ConversationsClient, useValue: conversations },
        { provide: UsersClient, useValue: users },
        { provide: OffersService, useValue: offers },
        { provide: AuthService, useValue: auth },
      ],
    });
    const fixture = TestBed.createComponent(ConversationPage);
    fixture.componentRef.setInput('conversationId', 'c1');
    fixture.componentInstance.ionViewWillEnter();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-empty-state h2')?.textContent).toContain(
      'Nie ma takiej rozmowy',
    );
  });
});
