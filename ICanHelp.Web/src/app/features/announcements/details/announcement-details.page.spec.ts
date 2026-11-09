import { Component, input, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { UserDto } from '../../../core/api/api-client';
import { AnnouncementsClient, CategoriesClient, UsersClient } from '../../../core/api/api-client';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/ui/toast.service';
import { MapComponent } from '../../../shared/components/map/map.component';
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
  const announcements = { getById: vi.fn() };
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
        { provide: UsersClient, useValue: users },
        { provide: CategoriesClient, useValue: { getAll: () => of([]) } },
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: toast },
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

    page.makeOffer();

    expect(router.serializeUrl(navigate.mock.calls[0][0] as never)).toBe('/login?returnUrl=%2Fannouncements%2Fa1');
    expect(toast.info).not.toHaveBeenCalled();
  });

  it('hides the offer actions from the owner', async () => {
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
    const { element, render } = create();
    await render();

    expect(element.querySelector('ion-footer')?.textContent).toContain('To Twoje ogłoszenie');
    expect(element.querySelector('.actions')).toBeNull();
  });

  it('shows a not-found state for a missing announcement', async () => {
    announcements.getById.mockReturnValue(throwError(() => ({ status: 404 })));
    const { element, render } = create();
    await render();

    expect(element.querySelector('app-empty-state h2')?.textContent).toContain('Nie ma takiego ogłoszenia');
    expect(element.querySelector('ion-footer')).toBeNull();
  });
});
