import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AnnouncementsClient, CategoriesClient } from '../../core/api/api-client';
import { GeolocationError, GeolocationService } from '../../core/geo/geolocation.service';
import { provideNotificationsStub } from '../../core/testing/notifications-stub';
import { ToastService } from '../../core/ui/toast.service';

import { AnnouncementsPage } from './announcements.page';
import { LikesService } from './likes/likes.service';
import { PAGE_SIZE } from './list/announcement-filters';
import { announcement } from './testing/announcement-fixtures';

describe('AnnouncementsPage', () => {
  const client = { getAll: vi.fn() };
  const categoriesClient = { getAll: vi.fn() };
  const geolocation = { getCurrentPosition: vi.fn() };
  const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };
  const likes = { load: () => of([]), isLiked: (id: string): boolean => id === 'liked-1' };

  function create(): { page: AnnouncementsPage; element: HTMLElement; render: () => Promise<void> } {
    TestBed.configureTestingModule({
      imports: [AnnouncementsPage],
      providers: [
        provideRouter([]),
        { provide: AnnouncementsClient, useValue: client },
        { provide: CategoriesClient, useValue: categoriesClient },
        { provide: GeolocationService, useValue: geolocation },
        { provide: ToastService, useValue: toast },
        { provide: LikesService, useValue: likes },
        provideHttpClient(),
        provideNotificationsStub(),
      ],
    });
    const fixture = TestBed.createComponent(AnnouncementsPage);
    fixture.detectChanges();
    return {
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: () => fixture.whenStable(),
    };
  }

  const titles = (element: HTMLElement): string[] =>
    Array.from(element.querySelectorAll('app-announcement-card h2')).map((title) => title.textContent?.trim() ?? '');

  beforeEach(() => {
    vi.resetAllMocks();
    categoriesClient.getAll.mockReturnValue(of([{ id: 'cat-home', name: 'Dom', subcategories: [] }]));
  });

  it('shows open announcements with their category', async () => {
    client.getAll.mockReturnValue(of([announcement('a1', { title: 'Cieknący kran' })]));
    const { element, render } = create();
    await render();

    expect(titles(element)).toEqual(['Cieknący kran']);
    expect(element.querySelector('app-announcement-card')?.textContent).toContain('Dom');
    expect(client.getAll).toHaveBeenCalledWith(
      undefined,
      undefined,
      undefined,
      'Open',
      undefined,
      undefined,
      undefined,
      undefined,
      1,
      PAGE_SIZE,
    );
  });

  it('reloads the list when the user comes back (e.g. an announcement was accepted meanwhile)', async () => {
    client.getAll.mockReturnValue(of([announcement('a1', { title: 'Kran' }), announcement('a2', { title: 'Płot' })]));
    const { page, element, render } = create();
    page.ionViewWillEnter();
    await render();
    expect(client.getAll).toHaveBeenCalledTimes(1);

    client.getAll.mockReturnValue(of([announcement('a2', { title: 'Płot' })]));
    page.ionViewWillEnter();
    await render();

    expect(client.getAll).toHaveBeenCalledTimes(2);
    expect(titles(element)).toEqual(['Płot']);
  });

  it('shows the number of offers and the heart of a liked announcement', async () => {
    client.getAll.mockReturnValue(
      of([announcement('liked-1', { helperCount: 3 }), announcement('a2', { helperCount: 0 })]),
    );
    const { element, render } = create();
    await render();

    const cards = element.querySelectorAll('app-announcement-card');
    expect(cards[0].querySelector('.offers')?.textContent).toContain('3 oferty');
    expect(cards[0].querySelector('.liked')).not.toBeNull();
    expect(cards[1].querySelector('.offers')).toBeNull();
    expect(cards[1].querySelector('.liked')).toBeNull();
  });

  it('asks for the position only after "near me" is clicked and shows distances', async () => {
    client.getAll.mockReturnValue(of([]));
    geolocation.getCurrentPosition.mockResolvedValue({ latitude: 53.84, longitude: 22.98 });
    const { page, element, render } = create();
    await render();
    expect(geolocation.getCurrentPosition).not.toHaveBeenCalled();

    client.getAll.mockReturnValue(of([announcement('n1', { distanceKm: 1.24 })]));
    await page.toggleNearMe();
    await render();

    expect(client.getAll).toHaveBeenLastCalledWith(
      undefined,
      undefined,
      undefined,
      'Open',
      53.84,
      22.98,
      10,
      undefined,
      1,
      PAGE_SIZE,
    );
    expect(element.querySelector('.distance')?.textContent).toContain('1,2 km');
  });

  it('turns "near me" off on the second click', async () => {
    client.getAll.mockReturnValue(of([]));
    geolocation.getCurrentPosition.mockResolvedValue({ latitude: 53.84, longitude: 22.98 });
    const { page } = create();

    await page.toggleNearMe();
    await page.toggleNearMe();

    expect(geolocation.getCurrentPosition).toHaveBeenCalledTimes(1);
    expect(client.getAll).toHaveBeenLastCalledWith(
      undefined,
      undefined,
      undefined,
      'Open',
      undefined,
      undefined,
      undefined,
      undefined,
      1,
      PAGE_SIZE,
    );
  });

  it('shows the geolocation error and keeps the list unfiltered', async () => {
    client.getAll.mockReturnValue(of([]));
    geolocation.getCurrentPosition.mockRejectedValue(new GeolocationError('Brak zgody na lokalizację.'));
    const { page } = create();

    await page.toggleNearMe();

    expect(toast.error).toHaveBeenCalledWith('Brak zgody na lokalizację.');
    expect(client.getAll).toHaveBeenCalledTimes(1);
  });

  it('offers clearing the filters when nothing matches', async () => {
    client.getAll.mockReturnValue(of([]));
    const { page, element, render } = create();

    page.setCategory('cat-home');
    await render();

    expect(element.querySelector('app-empty-state h2')?.textContent).toContain('Brak ogłoszeń dla tych filtrów');
    expect(client.getAll).toHaveBeenLastCalledWith(
      undefined,
      'cat-home',
      undefined,
      'Open',
      undefined,
      undefined,
      undefined,
      undefined,
      1,
      PAGE_SIZE,
    );
  });

  it('searches by phrase and toggles a category tile', async () => {
    client.getAll.mockReturnValue(of([]));
    const { page, element, render } = create();
    await render();

    const tiles = element.querySelectorAll<HTMLButtonElement>('.category-tile');
    expect(tiles).toHaveLength(1);
    expect(tiles[0].textContent).toContain('Dom');

    page.setPhrase('  kran ');
    expect(client.getAll).toHaveBeenLastCalledWith(
      undefined,
      undefined,
      undefined,
      'Open',
      undefined,
      undefined,
      undefined,
      'kran',
      1,
      PAGE_SIZE,
    );
    // Ta sama fraza (np. po spacji) – bez nowego żądania.
    const calls = client.getAll.mock.calls.length;
    page.setPhrase('kran ');
    expect(client.getAll).toHaveBeenCalledTimes(calls);

    tiles[0].click();
    await render();
    expect(client.getAll.mock.lastCall?.[1]).toBe('cat-home');
    expect(tiles[0].getAttribute('aria-pressed')).toBe('true');

    tiles[0].click();
    await render();
    expect(client.getAll.mock.lastCall?.[1]).toBeUndefined();

    page.clearFilters();
    expect(client.getAll).toHaveBeenLastCalledWith(
      undefined,
      undefined,
      undefined,
      'Open',
      undefined,
      undefined,
      undefined,
      undefined,
      1,
      PAGE_SIZE,
    );
  });
});
