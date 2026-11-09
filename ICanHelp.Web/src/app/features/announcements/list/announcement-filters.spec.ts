import { DEFAULT_FILTERS, PAGE_SIZE, activeFilterCount, toAnnouncementQuery } from './announcement-filters';

describe('toAnnouncementQuery', () => {
  it('asks only for open announcements, page by page', () => {
    expect(toAnnouncementQuery(DEFAULT_FILTERS, 3)).toEqual({ status: 'Open', page: 3, pageSize: PAGE_SIZE });
  });

  it('sends the location and radius only for "near me"', () => {
    const query = toAnnouncementQuery({ radiusKm: 5, near: { latitude: 53.84, longitude: 22.98 } }, 1);

    expect(query).toMatchObject({ latitude: 53.84, longitude: 22.98, radiusKm: 5 });
    expect(toAnnouncementQuery({ radiusKm: 5 }, 1).radiusKm).toBeUndefined();
  });

  it('ignores a subcategory without its category', () => {
    expect(toAnnouncementQuery({ radiusKm: 10, subcategoryId: 'sub' }, 1).subcategoryId).toBeUndefined();
    expect(toAnnouncementQuery({ radiusKm: 10, categoryId: 'cat', subcategoryId: 'sub' }, 1)).toMatchObject({
      categoryId: 'cat',
      subcategoryId: 'sub',
    });
  });
});

describe('activeFilterCount', () => {
  it('counts category, subcategory and location, not the radius', () => {
    expect(activeFilterCount(DEFAULT_FILTERS)).toBe(0);
    expect(activeFilterCount({ radiusKm: 25, categoryId: 'cat', near: { latitude: 1, longitude: 2 } })).toBe(2);
  });
});
