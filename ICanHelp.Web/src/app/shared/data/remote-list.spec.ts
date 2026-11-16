import { Subject, of, throwError } from 'rxjs';

import { PagedList, RemoteList } from './remote-list';

/** Zdarzenie Ionic (`ionRefresh` / `ionInfinite`) z celem, który trzeba zamknąć. */
function ionEvent(): { event: Event; complete: ReturnType<typeof vi.fn> } {
  const complete = vi.fn(() => Promise.resolve());
  return { event: { target: { complete } } as unknown as Event, complete };
}

describe('RemoteList', () => {
  it('shows the spinner only until the first result', () => {
    const response = new Subject<string[]>();
    const list = new RemoteList(() => response);

    list.reload();
    expect(list.state()).toBe('loading');

    response.next(['a']);
    expect(list.state()).toBe('ready');
    expect(list.items()).toEqual(['a']);

    list.reload();
    expect(list.state()).toBe('ready');
  });

  it('fails only when there is nothing to show', () => {
    const fetch = vi.fn(() => throwError(() => new Error('offline')));
    const list = new RemoteList<string>(fetch);
    const done = vi.fn();

    list.reload(done);
    expect(list.state()).toBe('failed');
    expect(done).toHaveBeenCalledTimes(1);

    list.items.set(['kept']);
    list.state.set('ready');
    list.reload(done);

    expect(list.state()).toBe('ready');
    expect(list.items()).toEqual(['kept']);
    expect(done).toHaveBeenCalledTimes(2);
  });

  it('closes the refresher after reloading', () => {
    const list = new RemoteList(() => of(['a']));
    const { event, complete } = ionEvent();

    list.refresh(event);

    expect(list.items()).toEqual(['a']);
    expect(complete).toHaveBeenCalledTimes(1);
  });
});

describe('PagedList', () => {
  const pages: Record<number, string[]> = { 1: ['a', 'b'], 2: ['b', 'c'], 3: [] };
  const fetch = vi.fn((page: number) => of(pages[page] ?? []));

  beforeEach(() => vi.clearAllMocks());

  it('loads the first page and appends the next ones without duplicates', () => {
    const list = new PagedList(fetch, (item) => item, 2);

    list.reload();
    expect(fetch).toHaveBeenLastCalledWith(1);
    expect(list.items()).toEqual(['a', 'b']);
    expect(list.complete()).toBe(false);

    const more = ionEvent();
    list.loadMore(more.event);
    expect(fetch).toHaveBeenLastCalledWith(2);
    expect(list.items()).toEqual(['a', 'b', 'c']);
    expect(list.complete()).toBe(false);
    expect(more.complete).toHaveBeenCalledTimes(1);

    list.loadMore(ionEvent().event);
    expect(fetch).toHaveBeenLastCalledWith(3);
    expect(list.complete()).toBe(true);
  });

  it('starts again from the first page after a reload', () => {
    const list = new PagedList(fetch, (item) => item, 2);
    list.reload();
    list.loadMore(ionEvent().event);

    list.reload();
    list.loadMore(ionEvent().event);

    expect(fetch.mock.calls.map(([page]) => page)).toEqual([1, 2, 1, 2]);
    expect(list.items()).toEqual(['a', 'b', 'c']);
  });

  it('keeps the list and closes the scroll when the next page fails', () => {
    const failing = vi.fn((page: number) => (page === 1 ? of(['a', 'b']) : throwError(() => new Error('offline'))));
    const list = new PagedList(failing, (item) => item, 2);
    list.reload();
    const more = ionEvent();

    list.loadMore(more.event);
    list.loadMore(ionEvent().event);

    expect(list.items()).toEqual(['a', 'b']);
    expect(more.complete).toHaveBeenCalledTimes(1);
    // Nieudana strona się nie liczy – kolejna próba prosi o tę samą.
    expect(failing.mock.calls.map(([page]) => page)).toEqual([1, 2, 2]);
  });
});
