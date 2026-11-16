import { signal } from '@angular/core';
import type { Observable } from 'rxjs';

import { completeIonEvent } from './ion-events';

/** Stan listy z API: pierwsze wczytanie, gotowa (także pusta), błąd bez danych do pokazania. */
export type ListState = 'loading' | 'ready' | 'failed';

/**
 * Lista wczytywana z API razem ze stanem dla ekranu. Spinner (`loading`) tylko wtedy, gdy nie ma jeszcze czego
 * pokazać; błąd psuje ekran (`failed`) tylko przy pustej liście – inaczej zostają dotychczasowe elementy
 * (komunikat pokazał już interceptor błędów). `items` można zmieniać z ekranu (np. usunięty wiersz).
 */
export class RemoteList<T> {
  readonly items = signal<T[]>([]);
  readonly state = signal<ListState>('loading');

  constructor(private readonly fetchAll: () => Observable<T[]>) {}

  /** Lista od nowa (wejście na ekran, zmiana filtra, „Spróbuj ponownie”); `done` – po sukcesie i po błędzie. */
  reload(done?: () => void): void {
    if (this.items().length === 0) {
      this.state.set('loading');
    }
    this.fetchAll().subscribe({
      next: (items) => {
        this.accept(items);
        this.state.set('ready');
        done?.();
      },
      error: () => {
        if (this.items().length === 0) {
          this.state.set('failed');
        }
        done?.();
      },
    });
  }

  /** `ion-refresher` („pociągnij, aby odświeżyć”): lista od nowa i zamknięcie wskaźnika. */
  refresh(event: Event): void {
    this.reload(() => completeIonEvent(event));
  }

  protected accept(items: T[]): void {
    this.items.set(items);
  }
}

/** Lista doładowywana stronami (`ion-infinite-scroll`): `reload` wczytuje pierwszą stronę, `loadMore` – kolejne. */
export class PagedList<T> extends RemoteList<T> {
  /** Ostatnia strona była niepełna – nie ma czego doładowywać. */
  readonly complete = signal(false);
  private page = 1;

  /**
   * @param fetchPage strona `page` (od 1) z API
   * @param key klucz elementu – między stronami mógł dojść nowy element i przesunąć wyniki, duplikaty pomijamy
   */
  constructor(
    private readonly fetchPage: (page: number) => Observable<T[]>,
    private readonly key: (item: T) => string,
    private readonly pageSize: number,
  ) {
    super(() => fetchPage(1));
  }

  /** `ion-infinite-scroll`: następna strona na koniec listy; błąd zostawia listę bez zmian. */
  loadMore(event: Event): void {
    this.fetchPage(this.page + 1).subscribe({
      next: (items) => {
        this.page += 1;
        this.items.update((current) => {
          const keys = new Set(current.map(this.key));
          return [...current, ...items.filter((item) => !keys.has(this.key(item)))];
        });
        this.complete.set(items.length < this.pageSize);
        completeIonEvent(event);
      },
      error: () => completeIonEvent(event),
    });
  }

  protected override accept(items: T[]): void {
    this.page = 1;
    super.accept(items);
    this.complete.set(items.length < this.pageSize);
  }
}
