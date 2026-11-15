import { signal } from '@angular/core';
import type { Observable } from 'rxjs';

import { errorStatus } from '../../core/api/api-errors';

import { completeIonEvent } from './ion-events';

/** Stan danych z API: pierwsze wczytanie, gotowe, nie istnieje (404 – tylko `RemoteItem`), błąd bez danych do pokazania. */
export type ItemState = 'loading' | 'ready' | 'not-found' | 'failed';

/**
 * Dane ekranu wczytywane z API jednym żądaniem (np. kilka list naraz) razem ze stanem wczytania. Błąd psuje ekran
 * (`failed`) tylko wtedy, gdy nic jeszcze nie wczytano – inaczej zostaje ostatnia wersja (komunikat pokazał już
 * interceptor błędów).
 */
export class RemoteData<T> {
  readonly value = signal<T | null>(null);
  readonly state = signal<ItemState>('loading');

  /**
   * @param fetch żądanie do API (wołane przy każdym `load`)
   * @param onLoaded po każdym udanym wczytaniu; `previous` – poprzednia wersja albo `null` (pierwsze wczytanie)
   */
  constructor(
    private readonly fetch: () => Observable<T>,
    private readonly onLoaded: (value: T, previous: T | null) => void = () => undefined,
  ) {}

  /** Wczytanie (wejście na ekran, po akcji, „Spróbuj ponownie”); `done` – po sukcesie i po błędzie. */
  load(done?: () => void): void {
    this.fetch().subscribe({
      next: (value) => {
        const previous = this.value();
        this.value.set(value);
        this.state.set('ready');
        this.onLoaded(value, previous);
        done?.();
      },
      error: (error: unknown) => {
        this.fail(error);
        done?.();
      },
    });
  }

  /** `ion-refresher` („pociągnij, aby odświeżyć”): wczytanie od nowa i zamknięcie wskaźnika. */
  refresh(event: Event): void {
    this.load(() => completeIonEvent(event));
  }

  protected fail(_error: unknown): void {
    if (!this.value()) {
      this.state.set('failed');
    }
  }
}

/**
 * Jeden zasób z API na ekranie (ogłoszenie, wątek, profil): jak `RemoteData`, a dodatkowo błąd 404 → `not-found`
 * (wartość znika – ekran pokazuje „nie ma takiego …”).
 */
export class RemoteItem<T> extends RemoteData<T> {
  protected override fail(error: unknown): void {
    if (errorStatus(error) === 404) {
      this.value.set(null);
      this.state.set('not-found');
    } else {
      super.fail(error);
    }
  }
}
