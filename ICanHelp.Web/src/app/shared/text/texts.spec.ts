import { announcementFieldLabel, announcementFieldTitle } from './announcement-fields';
import { capitalize } from './capitalize';
import { formatAverage, fromRatersSuffix, ratingsCountLabel } from './rating-texts';

describe('shared texts', () => {
  it('names announcement fields for sentences and for headings', () => {
    expect(announcementFieldLabel('Budget')).toBe('budżet');
    expect(announcementFieldTitle('Budget')).toBe('Budżet');
    // Nieznane pole (nowsza wersja backendu) zostaje bez tłumaczenia.
    expect(announcementFieldLabel('Deadline')).toBe('Deadline');
    expect(announcementFieldTitle('deadline')).toBe('deadline');
  });

  it('capitalizes the first letter only', () => {
    expect(capitalize('brak kontaktu')).toBe('Brak kontaktu');
    expect(capitalize('')).toBe('');
  });

  it('formats rating averages and counts in Polish', () => {
    expect(formatAverage(4.5)).toBe('4,5');
    expect(formatAverage(3)).toBe('3,0');
    expect(ratingsCountLabel(1)).toBe('1 ocena');
    expect(ratingsCountLabel(3)).toBe('3 oceny');
    expect(ratingsCountLabel(12)).toBe('12 ocen');
  });

  it('mentions the number of raters only when it is lower than the number of ratings', () => {
    expect(fromRatersSuffix(1, 3)).toBe(' od 1 osoby');
    expect(fromRatersSuffix(9, 12)).toBe(' od 9 osób');
    expect(fromRatersSuffix(3, 3)).toBe('');
  });
});
