import { Injectable, inject, signal } from '@angular/core';
import type { Subscription } from 'rxjs';
import { finalize } from 'rxjs';

import type { AnnouncementDto } from '../../../core/api/api-client';
import { AnnouncementsClient } from '../../../core/api/api-client';

import type { AnnouncementFilters } from './announcement-filters';
import { DEFAULT_FILTERS, PAGE_SIZE, toAnnouncementQuery } from './announcement-filters';

/**
 * Stan listy ogłoszeń: filtry, wczytane strony, doładowywanie (przewijanie bez końca).
 * Jeden na ekran (`providers` strony) – lista i mapa tego ekranu pokazują te same dane.
 */
@Injectable()
export class AnnouncementListStore {
  private readonly client = inject(AnnouncementsClient);
  private page = 0;
  private requestId = 0;
  private subscription: Subscription | null = null;

  readonly filters = signal<AnnouncementFilters>(DEFAULT_FILTERS);
  readonly items = signal<AnnouncementDto[]>([]);
  readonly loading = signal(false);
  /** Ostatnie żądanie się nie udało (komunikat pokazał już interceptor błędów). */
  readonly failed = signal(false);
  /** Pierwsza strona dla bieżących filtrów już przyszła (odróżnia „pusto” od „jeszcze nie wczytano”). */
  readonly loaded = signal(false);
  /** Ostatnia strona była niepełna – nie ma czego doładowywać. */
  readonly complete = signal(false);

  /** Zmiana filtrów = lista od pierwszej strony. */
  setFilters(patch: Partial<AnnouncementFilters>): Promise<void> {
    this.filters.update((filters) => ({ ...filters, ...patch }));
    this.loaded.set(false);
    this.items.set([]);
    return this.reload();
  }

  /** Pierwsza strona od nowa (np. „pociągnij, aby odświeżyć”). Dotychczasowe wyniki zostają do przyjścia nowych. */
  reload(): Promise<void> {
    return this.fetch(1);
  }

  /** Kolejna strona – nic nie robi, gdy coś się już wczytuje albo lista jest kompletna. */
  loadMore(): Promise<void> {
    if (this.loading() || this.complete()) {
      return Promise.resolve();
    }
    return this.fetch(this.page + 1);
  }

  /** Zwraca obietnicę spełnianą zawsze (sukces, błąd albo przerwanie przez nowsze żądanie) – dla ion-refresher / ion-infinite-scroll. */
  private fetch(page: number): Promise<void> {
    // Nowsze żądanie wygrywa: stare (np. sprzed zmiany filtrów) przerywamy, żeby jego wynik nie nadpisał listy.
    this.subscription?.unsubscribe();
    const id = ++this.requestId;
    const query = toAnnouncementQuery(this.filters(), page);
    this.loading.set(true);
    this.failed.set(false);

    return new Promise<void>((resolve) => {
      this.subscription = this.client
        .getAll(
          undefined,
          query.categoryId,
          query.subcategoryId,
          query.status,
          query.latitude,
          query.longitude,
          query.radiusKm,
          query.page,
          query.pageSize,
        )
        .pipe(
          finalize(() => {
            if (id === this.requestId) {
              this.loading.set(false);
            }
            resolve();
          }),
        )
        .subscribe({
          next: (result) => {
            this.items.update((items) => (page === 1 ? result : appendNew(items, result)));
            this.page = page;
            this.loaded.set(true);
            this.complete.set(result.length < PAGE_SIZE);
          },
          error: () => this.failed.set(true),
        });
    });
  }
}

/** Między stronami mogło dojść nowe ogłoszenie i przesunąć wyniki – bez duplikatów. */
function appendNew(items: AnnouncementDto[], next: AnnouncementDto[]): AnnouncementDto[] {
  const ids = new Set(items.map((item) => item.id));
  return [...items, ...next.filter((item) => !ids.has(item.id))];
}
