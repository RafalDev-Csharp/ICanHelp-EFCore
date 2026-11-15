import { APP_LOCALE } from '../../core/config/locale';

import { pluralPl } from './plural';

const averageFormat = new Intl.NumberFormat(APP_LOCALE, { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/** „4,5” – średnia ocen z jednym miejscem po przecinku. */
export function formatAverage(value: number): string {
  return averageFormat.format(value);
}

/** „1 ocena” / „3 oceny” / „5 ocen”. */
export function ratingsCountLabel(count: number): string {
  return `${count} ${pluralPl(count, 'ocena', 'oceny', 'ocen')}`;
}

/**
 * „ od 2 osób” – dopisek do liczby ocen, gdy oceniających jest mniej niż ocen (F11 H: jedna osoba = jeden głos);
 * inaczej pusty tekst.
 */
export function fromRatersSuffix(raters: number, ratings: number): string {
  return raters < ratings ? ` od ${raters} ${pluralPl(raters, 'osoby', 'osób', 'osób')}` : '';
}
