import { DEFAULT_CATEGORY_ICON, categoryIcon } from './category-icons';

describe('categoryIcon', () => {
  it('maps seeded categories by name, ignoring case', () => {
    expect(categoryIcon('Plumbing')).toBe('water-outline');
    expect(categoryIcon(' electrical ')).toBe('flash-outline');
  });

  it('falls back to the default icon', () => {
    expect(categoryIcon('Gardening')).toBe(DEFAULT_CATEGORY_ICON);
    expect(categoryIcon(undefined)).toBe(DEFAULT_CATEGORY_ICON);
  });
});
