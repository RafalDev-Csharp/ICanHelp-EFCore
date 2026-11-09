import type { AnnouncementDetailsDto, AnnouncementDto } from '../../../core/api/api-client';

/** Ogłoszenie z listy (`GET api/announcements`) do testów. */
export function announcement(id: string, overrides: Partial<AnnouncementDto> = {}): AnnouncementDto {
  return {
    id,
    ownerId: 'owner-1',
    categoryId: 'cat-home',
    subcategoryId: 'sub-plumbing',
    difficulty: 'Easy',
    title: `Ogłoszenie ${id}`,
    description: 'Cieknie kran w kuchni.',
    budget: { amount: 100, currency: 'PLN' },
    location: { latitude: 53.84, longitude: 22.98 },
    status: 'Open',
    createdAt: '2026-11-08T10:00:00Z',
    acceptedOfferId: undefined,
    revision: 0,
    updatedAt: undefined,
    thumbnailUrl: undefined,
    distanceKm: undefined,
    ...overrides,
  };
}

/** Szczegóły ogłoszenia (`GET api/announcements/{id}`) do testów. */
export function announcementDetails(
  id: string,
  overrides: Partial<AnnouncementDetailsDto> = {},
): AnnouncementDetailsDto {
  return { ...announcement(id), offers: [], helperCount: 0, images: [], changes: [], ...overrides };
}

/** `n` ogłoszeń o kolejnych id z prefiksem. */
export function announcements(prefix: string, count: number): AnnouncementDto[] {
  return Array.from({ length: count }, (_, index) => announcement(`${prefix}${index + 1}`));
}
