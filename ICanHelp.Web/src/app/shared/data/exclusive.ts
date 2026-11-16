import type { WritableSignal } from '@angular/core';

/**
 * Jedna akcja użytkownika naraz: w trakcie `busy` jest `true` (przyciski nieaktywne), a kolejne wywołanie – np.
 * drugie kliknięcie – jest pomijane (wynik `undefined`). `busy` wraca do `false` także po błędzie.
 */
export async function runExclusive<T>(busy: WritableSignal<boolean>, action: () => Promise<T>): Promise<T | undefined> {
  if (busy()) {
    return undefined;
  }
  busy.set(true);
  try {
    return await action();
  } finally {
    busy.set(false);
  }
}
