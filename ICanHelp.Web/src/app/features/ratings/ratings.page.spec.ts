import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { MyRatingDto, PendingRatingDto } from '../../core/api/api-client';
import { MyRatingsClient } from '../../core/api/api-client';
import { OffersService } from '../offers/offers.service';

import { RatingsPage } from './ratings.page';

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
    expect(element.querySelector('[data-testid="ratings-summary"]')?.textContent).toContain('4,5 · 2 oceny');
    expect(element.textContent).toContain('„Polecam.”');
    expect(element.textContent).toContain('@jan');

    page.selectTab('Client');
    await render();

    expect(element.querySelectorAll('[data-testid="received-rating"]')).toHaveLength(1);
    expect(element.querySelector('[data-testid="ratings-summary"]')?.textContent).toContain('3,0 · 1 ocena');
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
