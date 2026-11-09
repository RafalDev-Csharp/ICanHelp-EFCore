import { Injectable, computed, inject, signal } from '@angular/core';
import type { Observable } from 'rxjs';
import { finalize, of, shareReplay, tap } from 'rxjs';

import type { CategoryDto, SubcategoryDto } from '../api/api-client';
import { CategoriesClient } from '../api/api-client';

/**
 * Kategorie i podkategorie (`GET api/categories`). Zmienia je tylko seed backendu, więc wczytujemy raz na sesję
 * i dzielimy między ekranami (filtry listy, szczegóły, później formularz ogłoszenia i alerty).
 */
@Injectable({ providedIn: 'root' })
export class CategoriesService {
  private readonly client = inject(CategoriesClient);
  private readonly state = signal<CategoryDto[] | null>(null);
  private request: Observable<CategoryDto[]> | null = null;

  readonly categories = computed(() => this.state() ?? []);

  /** Id kategorii lub podkategorii → nazwa (Guidy są unikalne, więc jedna mapa wystarcza). */
  private readonly names = computed(() => {
    const names = new Map<string, string>();
    for (const category of this.categories()) {
      names.set(category.id, category.name);
      for (const subcategory of category.subcategories) {
        names.set(subcategory.id, subcategory.name);
      }
    }
    return names;
  });

  /** Wczytuje kategorie (raz; równoczesne wywołania dzielą jedno żądanie). Po błędzie kolejne wywołanie próbuje znowu. */
  load(): Observable<CategoryDto[]> {
    const loaded = this.state();
    if (loaded) {
      return of(loaded);
    }
    this.request ??= this.client.getAll().pipe(
      tap((categories) => this.state.set(categories)),
      finalize(() => (this.request = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.request;
  }

  /** Nazwa kategorii albo podkategorii; pusty tekst, gdy kategorie jeszcze się nie wczytały. */
  name(id: string | null | undefined): string {
    return id ? (this.names().get(id) ?? '') : '';
  }

  subcategoriesOf(categoryId: string | null | undefined): SubcategoryDto[] {
    return this.categories().find((category) => category.id === categoryId)?.subcategories ?? [];
  }
}
