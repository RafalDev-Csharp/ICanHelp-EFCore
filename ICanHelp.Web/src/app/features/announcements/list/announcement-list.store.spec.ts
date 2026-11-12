import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';

import type { AnnouncementDto } from '../../../core/api/api-client';
import { AnnouncementsClient } from '../../../core/api/api-client';
import { announcement, announcements } from '../testing/announcement-fixtures';

import { PAGE_SIZE } from './announcement-filters';
import { AnnouncementListStore } from './announcement-list.store';

describe('AnnouncementListStore', () => {
  const client = { getAll: vi.fn() };

  function create(): AnnouncementListStore {
    TestBed.configureTestingModule({
      providers: [AnnouncementListStore, { provide: AnnouncementsClient, useValue: client }],
    });
    return TestBed.inject(AnnouncementListStore);
  }

  beforeEach(() => client.getAll.mockReset());

  it('loads the first page of open announcements', async () => {
    client.getAll.mockReturnValue(of(announcements('a', 3)));
    const store = create();

    await store.reload();

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
    expect(store.items().map((item) => item.id)).toEqual(['a1', 'a2', 'a3']);
    expect(store.loaded()).toBe(true);
    expect(store.loading()).toBe(false);
    expect(store.complete()).toBe(true);
  });

  it('appends the next page without duplicates and stops after a short page', async () => {
    const store = create();
    client.getAll.mockReturnValueOnce(of(announcements('a', PAGE_SIZE)));
    await store.reload();
    expect(store.complete()).toBe(false);

    // Nowe ogłoszenie przesunęło wyniki – ostatnie z pierwszej strony przychodzi drugi raz.
    client.getAll.mockReturnValueOnce(of([announcement(`a${PAGE_SIZE}`), announcement('b1')]));
    await store.loadMore();

    expect(client.getAll.mock.calls[1][8]).toBe(2);
    expect(store.items()).toHaveLength(PAGE_SIZE + 1);
    expect(store.items().at(-1)?.id).toBe('b1');
    expect(store.complete()).toBe(true);

    await store.loadMore();
    expect(client.getAll).toHaveBeenCalledTimes(2);
  });

  it('starts from page one with the new filters', async () => {
    const store = create();
    client.getAll.mockReturnValue(of([announcement('a1')]));
    await store.reload();

    client.getAll.mockReturnValue(of([announcement('n1', { distanceKm: 1.2 })]));
    await store.setFilters({
      phrase: ' kran ',
      categoryId: 'cat',
      near: { latitude: 53.8, longitude: 23 },
      radiusKm: 5,
    });

    expect(client.getAll).toHaveBeenLastCalledWith(
      undefined,
      'cat',
      undefined,
      'Open',
      53.8,
      23,
      5,
      'kran',
      1,
      PAGE_SIZE,
    );
    expect(store.items().map((item) => item.id)).toEqual(['n1']);
  });

  it('ignores the result of a request replaced by a newer one', async () => {
    const store = create();
    const slow = new Subject<AnnouncementDto[]>();
    client.getAll.mockReturnValueOnce(slow);
    const first = store.reload();

    client.getAll.mockReturnValueOnce(of([announcement('new')]));
    await store.setFilters({ categoryId: 'cat' });
    slow.next([announcement('old')]);
    await first;

    expect(slow.observed).toBe(false);
    expect(store.items().map((item) => item.id)).toEqual(['new']);
    expect(store.loading()).toBe(false);
  });

  it('keeps the loaded items and marks the failure when a request fails', async () => {
    const store = create();
    client.getAll.mockReturnValueOnce(of(announcements('a', PAGE_SIZE)));
    await store.reload();

    client.getAll.mockReturnValueOnce(throwError(() => ({ status: 500 })));
    await store.loadMore();

    expect(store.failed()).toBe(true);
    expect(store.items()).toHaveLength(PAGE_SIZE);
    expect(store.loading()).toBe(false);
  });
});
