import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { MyRatingDto, PendingRatingDto } from '../../core/api/api-client';
import { MyRatingsClient } from '../../core/api/api-client';
import { OffersService } from '../offers/offers.service';

import { RatingsPage, groupByRater, groupedAverage } from './ratings.page';

function received(id: string, overrides: Partial<MyRatingDto> = {}): MyRatingDto {
  return {
    id,
    announcementId: 'a1',
    announcementTitle: 'Cieknący kran',
    myRole: 'Helper',
    rater: { id: 'u2', userName: 'jan' },
    rate: 5,
    comment: undefined,
    createdAt: '2026-11-12T10:00:00Z',
    ...overrides,
  };
}

function pendingItem(id: string, overrides: Partial<PendingRatingDto> = {}): PendingRatingDto {
  return {
    announcementId: id,
    announcementTitle: 'Malowanie płotu',
    thumbnailUrl: undefined,
    myRole: 'Client',
    otherParty: { id: 'u3', userName: 'pomocnik1' },
    createdAt: '2026-11-10T10:00:00Z',
    ...overrides,
  };
}

describe('RatingsPage', () => {
  const client = { getMine: vi.fn(), getPending: vi.fn() };
  const offers = { rate: vi.fn() };

  function create(): { page: RatingsPage; element: HTMLElement; render: () => Promise<void> } {
    TestBed.configureTestingModule({
      imports: [RatingsPage],
      providers: [
        provideRouter([]),
        { provide: MyRatingsClient, useValue: client },
        { provide: OffersService, useValue: offers },
      ],
    });
    const fixture = TestBed.createComponent(RatingsPage);
    fixture.componentInstance.ionViewWillEnter();
    return {
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: async () => {
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();
      },
    };
  }

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('starts on "to rate" and rates the other side, then reloads', async () => {
    client.getMine.mockReturnValue(of([]));
    client.getPending.mockReturnValue(of([pendingItem('a7'), pendingItem('a8', { myRole: 'Helper' })]));
    offers.rate.mockResolvedValue(true);
    const { page, element, render } = create();
    await render();

    expect(page.tab()).toBe('pending');
    const items = element.querySelectorAll('[data-testid="pending-rating"]');
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('Oceń pomocnika: pomocnik1');
    expect(items[1].textContent).toContain('Oceń zleceniodawcę');
    expect(element.textContent).toContain('Do wystawienia (2)');

    await page.rate(page.pending()[0]);

    expect(offers.rate).toHaveBeenCalledWith('a7', 'pomocnik1');
    expect(client.getPending).toHaveBeenCalledTimes(2);
  });

  it('does not reload when the rating window is closed', async () => {
    client.getMine.mockReturnValue(of([]));
    client.getPending.mockReturnValue(of([pendingItem('a7')]));
    offers.rate.mockResolvedValue(false);
    const { page, render } = create();
    await render();

    await page.rate(page.pending()[0]);

    expect(client.getPending).toHaveBeenCalledTimes(1);
  });

  it('splits received ratings by role, with the average', async () => {
    client.getMine.mockReturnValue(
      of([
        received('r1', { rate: 5, comment: 'Polecam.' }),
        received('r2', { rate: 4 }),
        received('r3', { myRole: 'Client', rate: 3 }),
      ]),
    );
    client.getPending.mockReturnValue(of([]));
    const { page, element, render } = create();
    await render();

    // Nic do wystawienia – od razu „Jako pomocnik”.
    expect(page.tab()).toBe('Helper');
    expect(element.querySelectorAll('[data-testid="received-rating"]')).toHaveLength(2);
    expect(element.querySelector('[data-testid="ratings-summary"]')?.textContent).toContain('4,5 · 2 oceny od 1 osoby');
    expect(element.textContent).toContain('„Polecam.”');
    expect(element.textContent).toContain('@jan');

    page.selectTab('Client');
    await render();

    expect(element.querySelectorAll('[data-testid="received-rating"]')).toHaveLength(1);
    expect(element.querySelector('[data-testid="ratings-summary"]')?.textContent).toContain('3,0 · 1 ocena');
  });

  it('groups ratings from the same person – one vote, the average of his ratings (F11 H)', async () => {
    client.getMine.mockReturnValue(
      of([
        received('r1', { rate: 2 }),
        received('r2', { rate: 5, rater: { id: 'u9', userName: 'ola' } }),
        received('r3', { rate: 4 }),
      ]),
    );
    client.getPending.mockReturnValue(of([]));
    const { element, render } = create();
    await render();

    // jan: 2 i 4 → 3; ola: 5 → (3 + 5) / 2 = 4 (a nie 11 / 3).
    expect(element.querySelector('[data-testid="ratings-summary"]')?.textContent).toContain('4,0 · 3 oceny od 2 osób');
    const groups = element.querySelectorAll('[data-testid="rater-group"]');
    expect(groups).toHaveLength(2);
    expect(groups[0].querySelector('[data-testid="rater-group-head"]')?.textContent).toContain(
      '@jan · Ocena grupowa · 2 oceny, średnio 3,0',
    );
    expect(groups[0].querySelectorAll('[data-testid="received-rating"]')).toHaveLength(2);
    expect(groups[1].querySelector('[data-testid="rater-group-head"]')).toBeNull();
    expect(groups[1].textContent).toContain('@ola');
  });

  it('groups by rater in the order of the newest rating', () => {
    const groups = groupByRater([
      received('r1', { rate: 1, rater: { id: 'u9', userName: 'ola' } }),
      received('r2', { rate: 2 }),
      received('r3', { rate: 4, rater: { id: 'u9', userName: 'ola' } }),
    ]);

    expect(groups.map((group) => [group.userName, group.average])).toEqual([
      ['ola', 2.5],
      ['jan', 2],
    ]);
    expect(groupedAverage(groups)).toBe(2.25);
    expect(groupedAverage([])).toBe(0);
  });

  it('keeps the chosen tab after reloading', async () => {
    client.getMine.mockReturnValue(of([]));
    client.getPending.mockReturnValue(of([pendingItem('a7')]));
    const { page, render } = create();
    await render();

    page.selectTab('Client');
    page.load();

    expect(page.tab()).toBe('Client');
  });

  it('shows an empty state per tab', async () => {
    client.getMine.mockReturnValue(of([]));
    client.getPending.mockReturnValue(of([]));
    const { page, element, render } = create();
    await render();

    expect(element.textContent).toContain('Brak ocen');
    page.selectTab('pending');
    await render();
    expect(element.textContent).toContain('Nic do oceny');
  });

  it('shows an error with a retry button', async () => {
    client.getMine.mockReturnValue(throwError(() => new Error('offline')));
    client.getPending.mockReturnValue(of([]));
    const { element, render } = create();
    await render();

    expect(element.textContent).toContain('Nie udało się wczytać ocen');
  });

  it('draws half stars for an average', () => {
    client.getMine.mockReturnValue(of([]));
    client.getPending.mockReturnValue(of([]));
    const { page } = create();

    expect([1, 2, 3, 4, 5].map((index) => page.starIcon(index, 3.5))).toEqual([
      'star',
      'star',
      'star',
      'star-half',
      'star-outline',
    ]);
  });
});
