import { Component, input, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AlertController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import type { UserDto } from '../../../core/api/api-client';
import { AnnouncementsClient, CategoriesClient, ConversationsClient, UsersClient } from '../../../core/api/api-client';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/ui/toast.service';
import { MapComponent } from '../../../shared/components/map/map.component';
import { OffersService } from '../../offers/offers.service';
import { myConversationFixture, offerFixture } from '../../offers/testing/offer-fixtures';
import { LikesService } from '../likes/likes.service';
import { announcementDetails } from '../testing/announcement-fixtures';

import { AnnouncementDetailsPage } from './announcement-details.page';

/** Leaflet w jsdom nie ma czego rysować – w testach strony mapę zastępuje pusta zaślepka. */
@Component({ selector: 'app-map', template: '' })
class MapStubComponent {
  readonly markers = input<unknown>();
  readonly center = input<unknown>();
  readonly zoom = input<unknown>();
  readonly interactive = input<unknown>();
}

describe('AnnouncementDetailsPage', () => {
  const announcements = {
    getById: vi.fn(),
    cancel: vi.fn(),
    start: vi.fn(),
    complete: vi.fn(),
    getContact: vi.fn(),
    getRatings: vi.fn(),
    postConversation: vi.fn(),
  };
  const conversations = { getMine: vi.fn() };
  const offers = { make: vi.fn(), rate: vi.fn(), start: vi.fn(), complete: vi.fn() };
  const likes = { load: vi.fn(() => of([])), isLiked: vi.fn(() => false), sync: vi.fn(), toggle: vi.fn() };
  /** Potwierdzenie z okna: `confirm` wybiera przycisk o tej roli. */
  let alertButtons: { role?: string; handler?: () => void }[] = [];
  /** Rola, którą „wybiera” użytkownik w potwierdzeniach czekających na `onDidDismiss` (start, zakończenie). */
  let alertRole = 'cancel';
  const alerts = {
    create: vi.fn((options: { buttons: { role?: string; handler?: () => void }[] }) => {
      alertButtons = options.buttons;
      return Promise.resolve({
        present: () => Promise.resolve(),
        onDidDismiss: () => Promise.resolve({ role: alertRole }),
      });
    }),
  };
  const users = { getById: vi.fn(), getRatings: vi.fn() };
  const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };
  const auth = {
    isAuthenticated: signal(false),
    currentUser: signal<UserDto | null>(null),
    loadCurrentUser: vi.fn(() => of()),
  };

  function create(): { page: AnnouncementDetailsPage; element: HTMLElement; render: () => Promise<void> } {
    TestBed.configureTestingModule({
      imports: [AnnouncementDetailsPage],
      providers: [
        provideRouter([]),
        { provide: AnnouncementsClient, useValue: announcements },
        { provide: ConversationsClient, useValue: conversations },
        { provide: OffersService, useValue: offers },
        { provide: UsersClient, useValue: users },
        { provide: CategoriesClient, useValue: { getAll: () => of([]) } },
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: toast },
        { provide: LikesService, useValue: likes },
        { provide: AlertController, useValue: alerts },
      ],
    });
    TestBed.overrideComponent(AnnouncementDetailsPage, {
      remove: { imports: [MapComponent] },
      add: { imports: [MapStubComponent] },
    });
    const fixture = TestBed.createComponent(AnnouncementDetailsPage);
    fixture.componentRef.setInput('announcementId', 'a1');
    fixture.componentInstance.ionViewWillEnter();
    fixture.detectChanges();
    return {
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: () => fixture.whenStable(),
    };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    auth.isAuthenticated.set(false);
    auth.currentUser.set(null);
    alertRole = 'cancel';
    conversations.getMine.mockReturnValue(of([]));
    announcements.getContact.mockReturnValue(of(null));
    announcements.getRatings.mockReturnValue(of([]));
    announcements.getById.mockReturnValue(
      of(
        announcementDetails('a1', {
          title: 'Powieszenie obrazu',
          budget: { amount: 80, currency: 'PLN' },
          helperCount: 2,
          images: [
            { id: 'i1', announcementId: 'a1', fileName: 'a.jpg', fileExtension: '.jpg', url: '/api/x/i1', addedAt: '' },
          ],
        }),
      ),
    );
    users.getById.mockReturnValue(
      of({
        id: 'owner-1',
        userName: 'jan',
        fullName: 'Jan Kowalski',
        description: undefined,
        city: 'Augustów',
        createdAt: '2026-01-10T00:00:00Z',
      }),
    );
    users.getRatings.mockReturnValue(
      of([
        {
          id: 'r1',
          announcementId: 'x',
          raterId: 'h',
          rateeId: 'owner-1',
          rate: 5,
          comment: 'Super!',
          createdAt: '2026-11-01T10:00:00Z',
        },
        {
          id: 'r2',
          announcementId: 'y',
          raterId: 'h',
          rateeId: 'owner-1',
          rate: 4,
          comment: undefined,
          createdAt: '2026-10-01T10:00:00Z',
        },
      ]),
    );
  });

  it('shows the announcement, photos and the owner with ratings', async () => {
    const { element, render } = create();
    await render();

    expect(announcements.getById).toHaveBeenCalledWith('a1');
    expect(element.querySelector('h1')?.textContent).toContain('Powieszenie obrazu');
    expect(element.querySelector('.budget')?.textContent?.replace(/\s/g, ' ')).toContain('80 zł');
    expect(element.querySelectorAll('.gallery img')).toHaveLength(1);
    expect(element.querySelector('.fact-difficulty')?.textContent).toContain('Trudność');
    expect(element.querySelector('.fact-offers')?.textContent).toContain('2 oferty');
    expect(element.querySelector('app-brand')).not.toBeNull();
    expect(element.textContent).toContain('Jan Kowalski');
    expect(element.textContent).toContain('4,5');
    expect(element.textContent).toContain('2 oceny');
    expect(element.querySelectorAll('.review-text')).toHaveLength(1);
  });

  it('sends a guest to sign in and back before making an offer', async () => {
    const { page, render } = create();
    await render();
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    vi.spyOn(router, 'url', 'get').mockReturnValue('/announcements/a1');

    await page.makeOffer();

    expect(router.serializeUrl(navigate.mock.calls[0][0] as never)).toBe('/login?returnUrl=%2Fannouncements%2Fa1');
    expect(offers.make).not.toHaveBeenCalled();
    expect(conversations.getMine).not.toHaveBeenCalled();
  });

  it('lets a helper make an offer and opens the thread', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'helper-1' } as UserDto);
    offers.make.mockResolvedValue('o1');
    announcements.postConversation.mockReturnValue(of({ id: 'c1' }));
    const { page, render } = create();
    await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    await page.makeOffer();

    expect(offers.make).toHaveBeenCalledWith('a1', 80);
    expect(navigate).toHaveBeenCalledWith(['/conversations', 'c1']);
  });

  it('shows the pending offer of a helper and leads to his thread', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'helper-1' } as UserDto);
    announcements.getById.mockReturnValue(of(announcementDetails('a1', { offers: [offerFixture('o1')] })));
    conversations.getMine.mockReturnValue(
      of([myConversationFixture('c1', { myRole: 'Helper', otherParty: { id: 'owner-1', userName: 'jan' } })]),
    );
    const { page, element, render } = create();
    await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    expect(conversations.getMine).toHaveBeenCalledWith(undefined, undefined, 'a1', 1, 50);
    expect(element.querySelector('ion-footer')?.textContent?.replace(/\s+/g, ' ')).toContain('Twoja oferta: 120 zł');
    expect(element.querySelector('.negotiations')?.textContent).toContain('Twoja negocjacja');

    await page.makeOffer();

    expect(offers.make).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/conversations', 'c1']);
  });

  it('gives the accepted helper the contact and the start action', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'helper-1' } as UserDto);
    announcements.getById.mockReturnValue(
      of(
        announcementDetails('a1', {
          status: 'Accepted',
          acceptedOfferId: 'o1',
          offers: [offerFixture('o1', { status: 'Accepted' })],
        }),
      ),
    );
    announcements.getContact.mockReturnValue(
      of({
        userId: 'owner-1',
        userName: 'jan',
        fullName: 'Jan Kowalski',
        contactData: {
          email: 'jan@example.com',
          phoneNumber: '+48600100200',
          country: 'PL',
          city: 'Augustów',
          postalCode: '16-300',
        },
      }),
    );
    offers.start.mockResolvedValue(true);
    const { page, element, render } = create();
    await render();

    expect(element.querySelector('.contact')?.textContent).toContain('+48600100200');
    const footer = element.querySelector('ion-footer')?.textContent ?? '';
    expect(footer).toContain('Rozpocznij');
    expect(footer).toContain('Rozmowa');

    await page.start();

    expect(offers.start).toHaveBeenCalledWith('a1');
    expect(announcements.getById).toHaveBeenCalledTimes(2);
  });

  it('lets the owner complete the job in progress and then rate', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
    announcements.getById.mockReturnValue(
      of(
        announcementDetails('a1', {
          status: 'InProgress',
          acceptedOfferId: 'o1',
          offers: [offerFixture('o1', { status: 'Accepted' })],
        }),
      ),
    );
    offers.complete.mockResolvedValue(true);
    const { page, element, render } = create();
    await render();

    expect(element.querySelector('ion-footer')?.textContent).toContain('Zakończ');
    await page.complete();
    expect(offers.complete).toHaveBeenCalledWith('a1');

    announcements.getById.mockReturnValue(
      of(
        announcementDetails('a1', {
          status: 'Completed',
          acceptedOfferId: 'o1',
          offers: [offerFixture('o1', { status: 'Accepted' })],
        }),
      ),
    );
    page.load();
    await render();

    expect(announcements.getRatings).toHaveBeenCalledWith('a1');
    expect(element.querySelector('ion-footer')?.textContent).toContain('Oceń pomocnika');
  });

  it('lists negotiations and the history for the owner', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
    announcements.getById.mockReturnValue(
      of(
        announcementDetails('a1', {
          offers: [offerFixture('o1')],
          changes: [
            {
              revision: 1,
              changedAt: '2026-11-09T12:00:00Z',
              fields: [{ field: 'Budget', oldValue: '100', newValue: '80' }],
            },
          ],
        }),
      ),
    );
    conversations.getMine.mockReturnValue(of([myConversationFixture('c1', { unreadCount: 1 })]));
    const { page, element, render } = create();
    await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    expect(element.querySelector('.negotiations')?.textContent).toContain('pomocnik1');
    const history = element.querySelector('.history')?.textContent ?? '';
    expect(history).toContain('Edycja: budżet');
    expect(history).toContain('Oferta (pomocnik1)');

    page.openConversation('c1');
    expect(navigate).toHaveBeenCalledWith(['/conversations', 'c1']);
  });

  it('gives the owner edit and cancel instead of the offer actions', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
    const { page, element, render } = create();
    await render();

    const footer = element.querySelector('ion-footer')?.textContent ?? '';
    expect(footer).toContain('Edytuj');
    expect(footer).toContain('Anuluj');
    expect(footer).not.toContain('Złóż ofertę');
    expect(element.querySelector('ion-header ion-buttons[slot="end"]')).toBeNull();

    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    page.edit();
    expect(navigate).toHaveBeenCalledWith(['/announcements', 'a1', 'edit']);
  });

  it('cancels the announcement only after confirmation', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
    announcements.cancel.mockReturnValue(of(undefined));
    const { page, render } = create();
    await render();

    await page.cancel();
    expect(announcements.cancel).not.toHaveBeenCalled();

    alertButtons.find((button) => button.role === 'destructive')?.handler?.();

    expect(announcements.cancel).toHaveBeenCalledWith('a1');
    expect(toast.success).toHaveBeenCalledWith('Ogłoszenie anulowane.');
    expect(announcements.getById).toHaveBeenCalledTimes(2);
  });

  it('likes the announcement of someone else', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'helper-1' } as UserDto);
    likes.toggle.mockReturnValue(of(true));
    const { page, render } = create();
    await render();

    expect(likes.sync).toHaveBeenCalledWith('a1', false);
    page.toggleLike();

    expect(likes.toggle).toHaveBeenCalledWith('a1');
    expect(toast.info).toHaveBeenCalledWith('Dodano do polubionych.');
  });

  it('sends a guest to sign in before liking', async () => {
    const { page, render } = create();
    await render();
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    page.toggleLike();

    expect(navigate).toHaveBeenCalled();
    expect(likes.toggle).not.toHaveBeenCalled();
  });

  it('shows a not-found state for a missing announcement', async () => {
    announcements.getById.mockReturnValue(throwError(() => ({ status: 404 })));
    const { element, render } = create();
    await render();

    expect(element.querySelector('app-empty-state h2')?.textContent).toContain('Nie ma takiego ogłoszenia');
    expect(element.querySelector('ion-footer')).toBeNull();
  });
});
