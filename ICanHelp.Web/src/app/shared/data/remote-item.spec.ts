import type { Observable } from 'rxjs';
import { of, throwError } from 'rxjs';

import { RemoteData, RemoteItem } from './remote-item';

describe('RemoteItem', () => {
  it('loads the value and tells what was there before', () => {
    let version = 0;
    const loaded = vi.fn();
    const item = new RemoteItem(() => of({ version: ++version }), loaded);
    expect(item.state()).toBe('loading');

    item.load();
    expect(item.state()).toBe('ready');
    expect(item.value()).toEqual({ version: 1 });
    expect(loaded).toHaveBeenLastCalledWith({ version: 1 }, null);

    item.load();
    expect(loaded).toHaveBeenLastCalledWith({ version: 2 }, { version: 1 });
  });

  it('turns a 404 into "not found" and forgets the value', () => {
    let response: Observable<string> = of('a');
    const item = new RemoteItem(() => response);
    item.load();

    response = throwError(() => ({ status: 404 }));
    item.load();

    expect(item.state()).toBe('not-found');
    expect(item.value()).toBeNull();
  });

  it('fails only when nothing was loaded – otherwise keeps the last version', () => {
    let response: Observable<string> = throwError(() => ({ status: 500 }));
    const item = new RemoteItem(() => response);
    const done = vi.fn();

    item.load(done);
    expect(item.state()).toBe('failed');

    response = of('a');
    item.load(done);
    response = throwError(() => ({ status: 0 }));
    item.load(done);

    expect(item.state()).toBe('ready');
    expect(item.value()).toBe('a');
    expect(done).toHaveBeenCalledTimes(3);
  });

  it('closes the refresher after loading', () => {
    const complete = vi.fn(() => Promise.resolve());
    const item = new RemoteItem(() => of('a'));

    item.refresh({ target: { complete } } as unknown as Event);

    expect(item.value()).toBe('a');
    expect(complete).toHaveBeenCalledTimes(1);
  });
});

describe('RemoteData', () => {
  it('treats a 404 like any other failure – there is no single resource to miss', () => {
    let response: Observable<string[]> = throwError(() => ({ status: 404 }));
    const data = new RemoteData(() => response);

    data.load();
    expect(data.state()).toBe('failed');

    response = of(['a']);
    data.load();
    response = throwError(() => ({ status: 404 }));
    data.load();

    expect(data.state()).toBe('ready');
    expect(data.value()).toEqual(['a']);
  });
});
