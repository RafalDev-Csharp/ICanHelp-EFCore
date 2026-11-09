import { TestBed } from '@angular/core/testing';
import { Subject, firstValueFrom, of, throwError } from 'rxjs';

import type { CategoryDto } from '../api/api-client';
import { CategoriesClient } from '../api/api-client';

import { CategoriesService } from './categories.service';

const CATEGORIES: CategoryDto[] = [
  {
    id: 'home',
    name: 'Dom',
    subcategories: [
      { id: 'plumbing', categoryId: 'home', name: 'Hydraulika' },
      { id: 'painting', categoryId: 'home', name: 'Malowanie' },
    ],
  },
  { id: 'advice', name: 'Porady', subcategories: [] },
];

describe('CategoriesService', () => {
  const client = { getAll: vi.fn() };

  function create(): CategoriesService {
    TestBed.configureTestingModule({ providers: [{ provide: CategoriesClient, useValue: client }] });
    return TestBed.inject(CategoriesService);
  }

  beforeEach(() => client.getAll.mockReset());

  it('loads once and shares one request between callers', async () => {
    const response = new Subject<CategoryDto[]>();
    client.getAll.mockReturnValue(response);
    const service = create();

    const first = firstValueFrom(service.load());
    const second = firstValueFrom(service.load());
    response.next(CATEGORIES);
    response.complete();

    expect(await first).toEqual(CATEGORIES);
    expect(await second).toEqual(CATEGORIES);
    await firstValueFrom(service.load());
    expect(client.getAll).toHaveBeenCalledTimes(1);
  });

  it('retries after a failed request', async () => {
    client.getAll.mockReturnValueOnce(throwError(() => ({ status: 0 })));
    const service = create();

    await expect(firstValueFrom(service.load())).rejects.toBeTruthy();
    client.getAll.mockReturnValueOnce(of(CATEGORIES));

    expect(await firstValueFrom(service.load())).toEqual(CATEGORIES);
  });

  it('finds names and subcategories', async () => {
    client.getAll.mockReturnValue(of(CATEGORIES));
    const service = create();
    expect(service.name('home')).toBe('');

    await firstValueFrom(service.load());

    expect(service.name('home')).toBe('Dom');
    expect(service.name('painting')).toBe('Malowanie');
    expect(service.name(undefined)).toBe('');
    expect(service.subcategoriesOf('home').map((subcategory) => subcategory.name)).toEqual(['Hydraulika', 'Malowanie']);
    expect(service.subcategoriesOf(undefined)).toEqual([]);
  });
});
