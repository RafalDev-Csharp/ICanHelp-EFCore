import type { PipeTransform } from '@angular/core';
import { Pipe } from '@angular/core';

import { APP_LOCALE } from '../../core/config/locale';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

const dateFormat = new Intl.DateTimeFormat(APP_LOCALE, { day: 'numeric', month: 'short' });
const dateWithYearFormat = new Intl.DateTimeFormat(APP_LOCALE, { day: 'numeric', month: 'short', year: 'numeric' });

/**
 * Daty z API są w UTC, ale bywają zapisane bez strefy („2026-11-08T10:00:00”) – wtedy przeglądarka
 * uznałaby je za czas lokalny. Brak strefy = UTC (tak samo jak w backendzie, `AsUtc()`).
 */
export function parseApiDate(value: string | Date): Date {
  if (value instanceof Date) {
    return value;
  }
  const hasZone = /(Z|[+-]\d{2}:?\d{2})$/i.test(value);
  const isDateOnly = /^\d{4}-\d{2}-\d{2}$/.test(value);
  return new Date(hasZone || isDateOnly ? value : `${value}Z`);
}

/** „przed chwilą”, „5 min temu”, „2 h temu”, „1 dzień temu”, „3 dni temu”, potem data („8 lis”, z rokiem gdy inny). */
export function formatTimeAgo(value: string | Date | null | undefined, now: Date = new Date()): string {
  if (value === null || value === undefined || value === '') {
    return '';
  }
  const date = parseApiDate(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  const diff = now.getTime() - date.getTime();
  if (diff < -MINUTE) {
    // Przyszłość (np. termin oferty) – sama data; drobne przesunięcie zegara traktujemy jak „teraz”.
    return formatDate(date, now);
  }
  if (diff < MINUTE) {
    return 'przed chwilą';
  }
  if (diff < HOUR) {
    return `${Math.floor(diff / MINUTE)} min temu`;
  }
  if (diff < DAY) {
    return `${Math.floor(diff / HOUR)} h temu`;
  }
  const days = Math.floor(diff / DAY);
  if (days === 1) {
    return '1 dzień temu';
  }
  if (days < 7) {
    return `${days} dni temu`;
  }
  return formatDate(date, now);
}

function formatDate(date: Date, now: Date): string {
  return date.getFullYear() === now.getFullYear() ? dateFormat.format(date) : dateWithYearFormat.format(date);
}

/** Czysty pipe – tekst liczy się przy zmianie wartości, nie odświeża się sam co minutę (na listach wystarcza). */
@Pipe({ name: 'timeAgo' })
export class TimeAgoPipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    return formatTimeAgo(value);
  }
}
