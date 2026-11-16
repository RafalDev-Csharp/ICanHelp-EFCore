import {
  DEFAULT_FILTERS,
  MAX_PHRASE_LENGTH,
  PAGE_SIZE,
  activeFilterCount,
  normalizePhrase,
  toAnnouncementQuery,
} from './announcement-filters';

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

  it('sends a trimmed phrase, and no phrase when blank', () => {
    expect(toAnnouncementQuery({ radiusKm: 10, phrase: '  kran ' }, 1).phrase).toBe('kran');
    expect(toAnnouncementQuery({ radiusKm: 10, phrase: '   ' }, 1)).not.toHaveProperty('phrase');
  });
});

describe('normalizePhrase', () => {
  it('trims, cuts to the API limit and turns blank into undefined', () => {
    expect(normalizePhrase(' płot ')).toBe('płot');
    expect(normalizePhrase('a'.repeat(MAX_PHRASE_LENGTH + 20))).toHaveLength(MAX_PHRASE_LENGTH);
    expect(normalizePhrase('')).toBeUndefined();
    expect(normalizePhrase(null)).toBeUndefined();
  });
});

describe('activeFilterCount', () => {
  it('counts phrase, category, subcategory and location, not the radius', () => {
    expect(activeFilterCount(DEFAULT_FILTERS)).toBe(0);
    expect(activeFilterCount({ radiusKm: 25, categoryId: 'cat', near: { latitude: 1, longitude: 2 } })).toBe(2);
    expect(activeFilterCount({ radiusKm: 10, phrase: 'kran' })).toBe(1);
    expect(activeFilterCount({ radiusKm: 10, phrase: ' ' })).toBe(0);
  });
});
