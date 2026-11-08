import { registerLocaleData } from '@angular/common';
import localePl from '@angular/common/locales/pl';
import type { EnvironmentProviders } from '@angular/core';
import { DEFAULT_CURRENCY_CODE, LOCALE_ID, makeEnvironmentProviders } from '@angular/core';

/** Język interfejsu. Używają go też pipe'y z `shared/pipes` (Intl). */
export const APP_LOCALE = 'pl-PL';
export const APP_CURRENCY = 'PLN';

/** Polskie daty, liczby i waluta dla wbudowanych pipe'ów Angulara (`date`, `currency`, `number`). */
export function provideAppLocale(): EnvironmentProviders {
  registerLocaleData(localePl);
  return makeEnvironmentProviders([
    { provide: LOCALE_ID, useValue: APP_LOCALE },
    { provide: DEFAULT_CURRENCY_CODE, useValue: APP_CURRENCY },
  ]);
}
