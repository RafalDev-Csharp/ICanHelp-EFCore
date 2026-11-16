import type { AlertDto, CategoryDto } from '../../../core/api/api-client';

/** Alert obszaru jak z `GET api/me/alerts` – w specs nadpisujemy pola. */
export function alertFixture(changes: Partial<AlertDto> = {}): AlertDto {
  return {
    id: 'al-1',
    name: 'Dom',
    kind: 'Area',
    categoryId: 'cat-home',
    subcategoryIds: [],
    center: { latitude: 53.844, longitude: 22.979 },
    routePoints: [],
    radiusKm: 5,
    schedule: { days: 'All', from: undefined, to: undefined },
    isEnabled: true,
    createdAt: '2026-11-10T10:00:00',
    updatedAt: undefined,
    ...changes,
  };
}

export const ALERT_CATEGORIES: CategoryDto[] = [
  {
    id: 'cat-home',
    name: 'Naprawy',
    subcategories: [
      { id: 'sub-plumbing', categoryId: 'cat-home', name: 'Hydraulika' },
      { id: 'sub-paint', categoryId: 'cat-home', name: 'Malowanie' },
    ],
  },
];
