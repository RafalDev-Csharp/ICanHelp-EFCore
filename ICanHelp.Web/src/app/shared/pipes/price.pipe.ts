import type { PipeTransform } from '@angular/core';
import { Pipe } from '@angular/core';

import { APP_CURRENCY, APP_LOCALE } from '../../core/config/locale';

const wholeFormat = new Intl.NumberFormat(APP_LOCALE, {
  style: 'currency',
  currency: APP_CURRENCY,
  maximumFractionDigits: 0,
});
const fractionFormat = new Intl.NumberFormat(APP_LOCALE, {
  style: 'currency',
  currency: APP_CURRENCY,
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** Cena w złotych: `150` → „150 zł”, `99.5` → „99,50 zł”, brak wartości → „”. */
export function formatPrice(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '';
  }
  return Number.isInteger(value) ? wholeFormat.format(value) : fractionFormat.format(value);
}

@Pipe({ name: 'price' })
export class PricePipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return formatPrice(value);
  }
}
