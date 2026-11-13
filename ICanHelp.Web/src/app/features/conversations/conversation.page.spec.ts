import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';

import type { ConversationDto, ConversationEntryDto, UserDto } from '../../core/api/api-client';
import { AnnouncementsClient, ConversationsClient, UsersClient } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import type { NotificationActivity } from '../../core/notifications/notifications.service';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { announcementDetails } from '../announcements/testing/announcement-fixtures';
import { OffersService } from '../offers/offers.service';
import { conversationFixture, offerFixture } from '../offers/testing/offer-fixtures';

import { ConversationPage } from './conversation.page';

describe('ConversationPage', () => {
  const conversations = { getById: vi.fn(), postMessage: vi.fn(), markAsRead: vi.fn() };
  const users = { getById: vi.fn(), getReliability: vi.fn() };
  const offers = {
    make: vi.fn(),
    accept: vi.fn(() => Promise.resolve(true)),
    reject: vi.fn(),
    counter: vi.fn(() => Promise.resolve(true)),
    withdraw: vi.fn(),
    revise: vi.fn(() => Promise.resolve(true)),
    reviewChanges: vi.fn(() => Promise.resolve(true)),
    showChanges: vi.fn(() => Promise.resolve()),
    start: vi.fn(() => Promise.resolve(true)),
    reportDone: vi.fn(() => Promise.resolve(true)),
    complete: vi.fn(() => Promise.resolve(true)),
    exitJob: vi.fn(() => Promise.resolve(true)),
    respondToExit: vi.fn(() => Promise.resolve(true)),
  };
  const announcements = { getById: vi.fn() };
  const auth = {
    currentUser: signal<UserDto | null>({ id: 'owner-1' } as UserDto),
    loadCurrentUser: vi.fn(() => of()),
  };
  const activity = new Subject<NotificationActivity>();
  const notifications = {
    activity$: activity.asObservable(),
    openConversation: vi.fn(),
    closeConversation: vi.fn(),
  };
  const providers = (): unknown[] => [
    provideRouter([]),
    { provide: ConversationsClient, useValue: conversations },
    { provide: AnnouncementsClient, useValue: announcements },
    { provide: UsersClient, useValue: users },
    { provide: OffersService, useValue: offers },
    { provide: AuthService, useValue: auth },
    { provide: NotificationsService, useValue: notifications },
  ];

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
    TestBed.configureTestingModule({ imports: [ConversationPage], providers: providers() });
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
    users.getReliability.mockReturnValue(of({ userId: 'helper-1', abandonedCount: 0, interruptedCount: 0 }));
    conversations.markAsRead.mockReturnValue(of(undefined));
    announcements.getById.mockReturnValue(
      of(
        announcementDetails('a1', {
          images: [
            { id: 'i1', announcementId: 'a1', fileName: 'a.jpg', fileExtension: '.jpg', url: '/api/x/i1', addedAt: '' },
          ],
        }),
      ),
    );
  });

  it('shows the announcement card with a photo and a link to the announcement', async () => {
    const { element, render } = create(conversationFixture());
    await render();

    expect(announcements.getById).toHaveBeenCalledWith('a1');
    expect(element.querySelector('.subject h2')?.textContent).toContain('Cieknący kran');
    expect(element.querySelector('.subject-thumb img')?.getAttribute('src')).toContain('/api/x/i1');
    expect(element.querySelector('.subject-link')?.textContent).toContain('Zobacz ogłoszenie');
  });

  it('keeps the thread working when the announcement photo cannot be loaded', async () => {
    announcements.getById.mockReturnValue(throwError(() => ({ status: 500 })));
    const { element, render } = create(conversationFixture());
    await render();

    expect(element.querySelector('.subject-thumb img')).toBeNull();
    expect(element.querySelector('.subject h2')?.textContent).toContain('Cieknący kran');
  });

  it('lets only the helper start the accepted job; the client waits but can confirm', async () => {
    const accepted = [offerEntry({ status: 'Accepted' })];
    const helper = create(conversationFixture({ announcementStatus: 'Accepted', timeline: accepted }));
    await helper.render();

    expect(helper.page.jobAction()).toBe('start');
    expect(helper.element.querySelector('.job-action')?.textContent).toContain('Rozpocznij zlecenie');
    await helper.page.runJobAction();
    expect(offers.start).toHaveBeenCalledWith('a1');
    expect(conversations.getById).toHaveBeenCalledTimes(2);
    TestBed.resetTestingModule();

    const client = create(
      conversationFixture({ myRole: 'Client', announcementStatus: 'Accepted', timeline: accepted }),
    );
    await client.render();
    expect(client.element.querySelector('.job-note')?.textContent).toContain('pomocnik rozpocznie');
    expect(client.element.querySelector('.job-action')?.textContent).toContain('Potwierdź wykonanie');
    expect(offers.start).toHaveBeenCalledTimes(1);
  });

  it('lets the helper report the work as done and then shows that he waits', async () => {
    const accepted = [offerEntry({ status: 'Accepted' })];
    const helper = create(conversationFixture({ announcementStatus: 'InProgress', timeline: accepted }));
    await helper.render();

    expect(helper.element.querySelector('.job-action')?.textContent).toContain('Zgłoś wykonanie');
    await helper.page.runJobAction();
    expect(offers.reportDone).toHaveBeenCalledWith('a1');
    TestBed.resetTestingModule();

    const waiting = create(
      conversationFixture({
        announcementStatus: 'InProgress',
        announcementDoneReportedAt: '2026-11-13T10:00:00Z',
        timeline: accepted,
      }),
    );
    await waiting.render();
    expect(waiting.element.querySelector('.job-action')).toBeNull();
    expect(waiting.element.querySelector('.job-note')?.textContent).toContain('czekasz na potwierdzenie');
  });

  it('highlights the confirmation for the client after the helper reported the work', async () => {
    const client = create(
      conversationFixture({
        myRole: 'Client',
        announcementStatus: 'InProgress',
        announcementDoneReportedAt: '2026-11-13T10:00:00Z',
        timeline: [offerEntry({ status: 'Accepted' })],
      }),
    );
    await client.render();

    expect(client.element.querySelector('.job-note.highlight')?.textContent).toContain('Pomocnik zgłosił wykonanie');
    expect(client.element.querySelector('.job-action')?.textContent).toContain('Potwierdź wykonanie');
    await client.page.runJobAction();
    expect(offers.complete).toHaveBeenCalledWith('a1');
  });

  it('has no job actions in a thread whose offer was not accepted', async () => {
    const { page, render } = create(
      conversationFixture({ announcementStatus: 'Accepted', timeline: [offerEntry({ status: 'Rejected' })] }),
    );
    await render();

    expect(page.jobAction()).toBeNull();
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

  it('tells notifications the thread is open and reloads on a live notification about it', async () => {
    const { page, render } = create(conversationFixture());
    await render();
    expect(notifications.openConversation).toHaveBeenCalledWith('c1');
    expect(conversations.getById).toHaveBeenCalledTimes(1);

    activity.next({ announcementId: 'other-a', conversationId: 'other' });
    expect(conversations.getById).toHaveBeenCalledTimes(1);

    activity.next({ announcementId: 'a1', conversationId: 'c1' });
    expect(conversations.getById).toHaveBeenCalledTimes(2);

    // Push / hub o tym ogłoszeniu bez wątku (np. przyszło tylko id ogłoszenia) – też przeładowanie.
    activity.next({ announcementId: 'a1' });
    expect(conversations.getById).toHaveBeenCalledTimes(3);

    page.ionViewWillEnter();
    expect(conversations.getById).toHaveBeenCalledTimes(4);

    page.ionViewWillLeave();
    expect(notifications.closeConversation).toHaveBeenCalledWith('c1');
  });

  const changes = [{ field: 'Budget', oldValue: '150.00 PLN', newValue: '200.00 PLN' }];
  const changeEntry = {
    type: 'AnnouncementChanged',
    at: '2026-11-09T12:00:00Z',
    change: { revision: 1, changedAt: '2026-11-09T12:00:00Z', fields: changes },
  } as ConversationEntryDto;

  it('shows the owner a calm note instead of "outdated" for an offer before his edit', async () => {
    const { element, render } = create(
      conversationFixture({
        myRole: 'Client',
        timeline: [offerEntry({ isOutdated: true, changesSinceOffer: changes }), changeEntry],
      }),
    );
    await render();

    expect(element.textContent).not.toContain('Nieaktualna');
    expect(element.querySelector('.offer ion-badge')?.textContent).toContain('Czeka na odpowiedź na zmiany');
    expect(element.querySelector('.offer-changes')?.textContent).toContain('Czeka na odpowiedź pomocnika na zmiany');
    const buttons = [...element.querySelectorAll('.offer-actions ion-button')].map((b) => b.textContent?.trim());
    expect(buttons).toEqual(['Odrzuć', 'Zaproponuj kontrofertę']);
    // Wpis edycji: „Edytowałeś ogłoszenie · data, godzina” + „zapisano”.
    const change = element.querySelector('.change.mine')?.textContent ?? '';
    expect(change).toContain('Edytowałeś ogłoszenie: budżet');
    expect(change).toContain('zapisano');
    expect(element.querySelector('.notice')).toBeNull();
    expect(conversations.markAsRead).not.toHaveBeenCalled();
  });

  it('lets the helper see the changes first and then keep, change or withdraw the offer', async () => {
    const outdated = offerEntry({ isOutdated: true, changesSinceOffer: changes });
    const { page, element, render } = create(
      conversationFixture({ myRole: 'Helper', timeline: [outdated, changeEntry] }),
    );
    await render();

    expect(element.querySelector('.offer-changes')?.textContent).toContain('Zleceniodawca zmienił ogłoszenie');
    const buttons = [...element.querySelectorAll('.offer-actions ion-button')].map((b) => b.textContent?.trim());
    expect(buttons).toEqual(['Zobacz zmiany']);
    expect(element.querySelector('.notice')?.textContent).toContain('Zleceniodawca zmienił ogłoszenie – zobacz zmiany');
    expect(element.querySelector('.change')?.textContent).toContain('Zleceniodawca zmienił ogłoszenie: budżet');

    await page.openNotice(page.notice()!);

    expect(offers.reviewChanges).toHaveBeenCalledWith(outdated.offer);
    expect(conversations.getById).toHaveBeenCalledTimes(2);
  });

  it('shows one edit of the announcement on its own', async () => {
    const { page, render } = create(conversationFixture({ myRole: 'Helper', timeline: [changeEntry] }));
    await render();

    await page.showChange(changes);

    expect(offers.showChanges).toHaveBeenCalledWith(changes);
  });

  it('collapses the previous version and points the receiver to the changed offer', async () => {
    const previous = {
      type: 'Offer',
      at: '2026-11-09T11:00:00Z',
      offer: offerFixture('o1', { status: 'Revised' }),
    } as ConversationEntryDto;
    const current = {
      type: 'Offer',
      at: '2026-11-09T13:00:00Z',
      offer: offerFixture('o2', { parentOfferId: 'o1', price: { amount: 150, currency: 'PLN' } }),
    } as ConversationEntryDto;
    const { page, element, render } = create(conversationFixture({ myRole: 'Client', timeline: [previous, current] }));
    await render();

    expect(element.querySelector('details.previous summary')?.textContent).toContain('Poprzednia wersja');
    expect(element.querySelectorAll('article.offer')).toHaveLength(1);
    expect(element.querySelector('.notice')?.textContent).toContain('Pomocnik zmienił ofertę – zobacz aktualną');

    await page.openNotice(page.notice()!);
    await render();

    expect(page.highlightedOfferId()).toBe('o2');
    expect(element.querySelector('article.offer.highlight')?.id).toBe('offer-o2');
  });

  it('tells the owner when the helper kept his offer after the changes', async () => {
    const previous = {
      type: 'Offer',
      at: '2026-11-09T11:00:00Z',
      offer: offerFixture('o1', { status: 'Revised' }),
    } as ConversationEntryDto;
    const kept = { type: 'Offer', at: '2026-11-09T13:00:00Z', offer: offerFixture('o2', { parentOfferId: 'o1' }) };
    const { element, render } = create(
      conversationFixture({ myRole: 'Client', timeline: [previous, kept as ConversationEntryDto] }),
    );
    await render();

    expect(element.querySelector('.notice')?.textContent).toContain('Pomocnik podtrzymał ofertę po zmianach');
  });

  it('lets the author change an up-to-date pending offer and reloads the thread', async () => {
    const pending = offerEntry();
    const { page, element, render } = create(conversationFixture({ myRole: 'Helper', timeline: [pending] }));
    await render();

    const buttons = [...element.querySelectorAll('.offer-actions ion-button')].map((b) => b.textContent?.trim());
    expect(buttons).toEqual(['Wycofaj', 'Zmień ofertę']);

    await page.revise(pending.offer);

    expect(offers.revise).toHaveBeenCalledWith(pending.offer);
    expect(conversations.getById).toHaveBeenCalledTimes(2);
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

  it('shows a thread without offers as questions and answers', async () => {
    const { page, element, render } = create(
      conversationFixture({ myRole: 'Helper', announcementStatus: 'Open', timeline: [messageEntry] }),
    );
    await render();

    expect(page.questionsOnly()).toBe(true);
    expect(element.querySelector('.questions-note')?.textContent).toContain('Pytania i odpowiedzi');
    const textarea = element.querySelector('ion-textarea') as (HTMLElement & { placeholder?: string }) | null;
    expect(textarea?.placeholder).toBe('Zadaj pytanie…');
    expect(element.querySelector('ion-footer')?.textContent).toContain('Złóż ofertę');
  });

  it('is read-only when the conversation is closed', async () => {
    const { element, render } = create(conversationFixture({ announcementStatus: 'Completed', canWrite: false }));
    await render();

    expect(element.querySelector('ion-textarea')).toBeNull();
    expect(element.querySelector('ion-footer')?.textContent).toContain('tylko do odczytu');
  });

  it('shows a not-found state for a foreign thread', async () => {
    conversations.getById.mockReturnValue(throwError(() => ({ status: 404 })));
    TestBed.configureTestingModule({ imports: [ConversationPage], providers: providers() });
    const fixture = TestBed.createComponent(ConversationPage);
    fixture.componentRef.setInput('conversationId', 'c1');
    fixture.componentInstance.ionViewWillEnter();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-empty-state h2')?.textContent).toContain(
      'Nie ma takiej rozmowy',
    );
  });
  // ---------- F11 E ----------

  const exitEntry = (overrides = {}): ConversationEntryDto =>
    ({
      type: 'JobExit',
      at: '2026-11-10T10:00:00Z',
      jobExit: {
        number: 1,
        kind: 'Abandoned',
        offerId: 'o1',
        helperId: 'helper-1',
        actorId: 'owner-1',
        actorRole: 'Client',
        reason: 'NoContact',
        note: 'Nie odbiera.',
        occurredAt: '2026-11-10T10:00:00Z',
        response: undefined,
        respondedAt: undefined,
        ...overrides,
      },
    }) as ConversationEntryDto;

  it('shows the abandonment in the timeline and closes the thread of that helper', async () => {
    auth.currentUser.set({ id: 'helper-1' } as UserDto);
    const { element, render } = create(
      conversationFixture({
        canWrite: false,
        timeline: [offerEntry({ status: 'Abandoned' }), exitEntry()],
      }),
    );
    await render();

    expect(element.querySelector('.change.exit')?.textContent).toContain('Zleceniodawca zrezygnował ze zlecenia');
    expect(element.querySelector('.change.exit')?.textContent).toContain('Brak kontaktu – Nie odbiera.');
    const footer = element.querySelector('ion-footer')?.textContent ?? '';
    expect(footer).toContain('Zlecenie zerwane przed rozpoczęciem');
    expect(footer).not.toContain('Złóż ofertę');
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
  });

  it('lets a participant interrupt the work in progress', async () => {
    const { page, element, render } = create(
      conversationFixture({
        myRole: 'Client',
        announcementStatus: 'InProgress',
        timeline: [offerEntry({ status: 'Accepted' })],
      }),
    );
    await render();

    expect(element.querySelector('.job-exit')?.textContent).toContain('Przerwij zlecenie');
    await page.exitJob();
    expect(offers.exitJob).toHaveBeenCalledWith('a1', 'interrupt', 'Pomocnik');
  });

  it('after an interruption shows the settlement note and the read-only reason', async () => {
    const { element, render } = create(
      conversationFixture({
        myRole: 'Client',
        announcementStatus: 'Interrupted',
        canWrite: false,
        timeline: [
          offerEntry({ status: 'Accepted' }),
          exitEntry({ kind: 'Interrupted', actorId: 'helper-1', actorRole: 'Helper' }),
        ],
      }),
    );
    await render();

    const footer = element.querySelector('ion-footer')?.textContent ?? '';
    expect(footer).toContain('Ustalcie między sobą rozliczenie');
    expect(footer).toContain('Zlecenie przerwane – rozmowa jest tylko do odczytu');
    expect(footer).toContain('Bez ocen');
    expect(element.querySelector('.job-exit')).toBeNull();
  });

  it('lets the other side add its statement to the interruption, once', async () => {
    const { page, element, render } = create(
      conversationFixture({
        myRole: 'Client',
        announcementStatus: 'Interrupted',
        canWrite: false,
        timeline: [
          offerEntry({ status: 'Accepted' }),
          exitEntry({ kind: 'Interrupted', actorId: 'helper-1', actorRole: 'Helper' }),
        ],
      }),
    );
    await render();

    expect(element.querySelector('.respond-action')?.textContent).toContain('Dodaj swoje stanowisko');
    await page.respondToExit(1);
    expect(offers.respondToExit).toHaveBeenCalledWith('a1', 1);
    expect(element.querySelector('.profile-link')?.getAttribute('href')).toBe('/users/helper-1');
  });

  it('shows the statement instead of the button once it is given', async () => {
    auth.currentUser.set({ id: 'helper-1' } as UserDto);
    const { element, render } = create(
      conversationFixture({
        canWrite: false,
        timeline: [offerEntry({ status: 'Abandoned' }), exitEntry({ response: 'Zmieniłem zdanie, przepraszam.' })],
      }),
    );
    await render();

    expect(element.querySelector('.respond-action')).toBeNull();
    expect(element.querySelector('.exit-response')?.textContent).toContain(
      'Stanowisko pomocnika: Zmieniłem zdanie, przepraszam.',
    );
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
  });

  it('shows the public counter of the other party', async () => {
    users.getReliability.mockReturnValue(of({ userId: 'helper-1', abandonedCount: 1, interruptedCount: 0 }));
    const { element, render } = create(conversationFixture({ myRole: 'Client' }));
    await render();

    expect(users.getReliability).toHaveBeenCalledWith('helper-1');
    expect(element.querySelector('.reliability')?.textContent).toContain('Pomocnik: 1 rezygnacja');
  });
});
