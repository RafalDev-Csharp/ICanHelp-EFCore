import { signal } from '@angular/core';

import { runExclusive } from './exclusive';

describe('runExclusive', () => {
  it('marks the action as busy and returns its result', async () => {
    const busy = signal(false);
    let finish: (value: string) => void = () => undefined;
    const action = vi.fn(() => new Promise<string>((resolve) => (finish = resolve)));

    const first = runExclusive(busy, action);
    expect(busy()).toBe(true);

    // Drugie kliknięcie w trakcie – pominięte.
    await expect(runExclusive(busy, action)).resolves.toBeUndefined();
    expect(action).toHaveBeenCalledTimes(1);

    finish('done');
    await expect(first).resolves.toBe('done');
    expect(busy()).toBe(false);
  });

  it('is free again after a failure', async () => {
    const busy = signal(false);

    await expect(runExclusive(busy, () => Promise.reject(new Error('boom')))).rejects.toThrow('boom');

    expect(busy()).toBe(false);
  });
});
