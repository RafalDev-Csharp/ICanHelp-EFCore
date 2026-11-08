import { formatDistance } from './distance.pipe';
import { formatPrice } from './price.pipe';
import { formatTimeAgo, parseApiDate } from './time-ago.pipe';

// Intl wstawia twarde spacje (U+00A0, U+202F) – w testach porównujemy ze zwykłymi.
const plain = (text: string): string => text.replace(/\s/g, ' ');

describe('formatPrice', () => {
  it('formats whole and fractional amounts in PLN', () => {
    expect(plain(formatPrice(150))).toBe('150 zł');
    expect(plain(formatPrice(99.5))).toBe('99,50 zł');
    expect(plain(formatPrice(15000))).toBe('15 000 zł');
  });

  it('returns empty text for missing values', () => {
    expect(formatPrice(null)).toBe('');
    expect(formatPrice(undefined)).toBe('');
    expect(formatPrice(Number.NaN)).toBe('');
  });
});

describe('formatDistance', () => {
  it('uses meters below 1 km', () => {
    expect(formatDistance(0.354)).toBe('350 m');
    expect(formatDistance(0)).toBe('10 m');
  });

  it('uses one decimal below 10 km and whole km above', () => {
    expect(formatDistance(0.9996)).toBe('1 km');
    expect(formatDistance(2.46)).toBe('2,5 km');
    expect(formatDistance(3)).toBe('3 km');
    expect(formatDistance(12.7)).toBe('13 km');
  });

  it('returns empty text for missing or invalid values', () => {
    expect(formatDistance(null)).toBe('');
    expect(formatDistance(-1)).toBe('');
  });
});

describe('formatTimeAgo', () => {
  const now = new Date('2026-11-08T12:00:00Z');
  const ago = (ms: number): Date => new Date(now.getTime() - ms);

  it('describes recent moments', () => {
    expect(formatTimeAgo(ago(30_000), now)).toBe('przed chwilą');
    expect(formatTimeAgo(ago(5 * 60_000), now)).toBe('5 min temu');
    expect(formatTimeAgo(ago(2 * 3_600_000), now)).toBe('2 h temu');
    expect(formatTimeAgo(ago(30 * 3_600_000), now)).toBe('1 dzień temu');
    expect(formatTimeAgo(ago(3 * 86_400_000), now)).toBe('3 dni temu');
  });

  it('shows a date for older and future moments', () => {
    expect(formatTimeAgo(ago(10 * 86_400_000), now)).toContain('paź');
    expect(formatTimeAgo('2025-01-15T10:00:00Z', now)).toContain('2025');
    expect(formatTimeAgo('2026-11-20T10:00:00Z', now)).toContain('lis');
  });

  it('treats API dates without a time zone as UTC', () => {
    expect(parseApiDate('2026-11-08T11:00:00').toISOString()).toBe('2026-11-08T11:00:00.000Z');
    expect(formatTimeAgo('2026-11-08T11:00:00', now)).toBe('1 h temu');
  });

  it('returns empty text for missing or invalid values', () => {
    expect(formatTimeAgo(null, now)).toBe('');
    expect(formatTimeAgo('nie-data', now)).toBe('');
  });
});
