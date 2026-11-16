import type { PipeTransform } from '@angular/core';
import { Pipe } from '@angular/core';

import { APP_LOCALE } from '../../core/config/locale';

const kmFormat = new Intl.NumberFormat(APP_LOCALE, { maximumFractionDigits: 1 });

/**
 * Odległość podana w kilometrach (jak w API): `0.35` → „350 m”, `2.46` → „2,5 km”, `12.7` → „13 km”.
 * Poniżej 1 km – metry zaokrąglone do 10 m (minimum „10 m”), do 10 km – jedno miejsce po przecinku, dalej – całe km.
 */
export function formatDistance(km: number | null | undefined): string {
  if (km === null || km === undefined || !Number.isFinite(km) || km < 0) {
    return '';
  }
  const meters = Math.max(10, Math.round((km * 1000) / 10) * 10);
  if (meters < 1000) {
    return `${meters} m`;
  }
  const value = km < 10 ? km : Math.round(km);
  return `${kmFormat.format(value)} km`;
}

@Pipe({ name: 'distance' })
export class DistancePipe implements PipeTransform {
  transform(km: number | null | undefined): string {
    return formatDistance(km);
  }
}
