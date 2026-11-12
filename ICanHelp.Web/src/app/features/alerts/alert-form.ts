import type {
  AlertDto,
  AlertKind,
  AlertScheduleDto,
  CreateAlert,
  LocationDto,
  UpdateAlert,
  WeekDays,
} from '../../core/api/api-client';
import type { GeoPoint } from '../../core/geo/geo-point';

/** Reguły jak w backendzie (`AnnouncementAlert`, `StringObject`). */
export const ALERT_RULES = {
  maxPerUser: 10,
  name: { max: 100 },
  radiusKm: { min: 0.5, max: 50, step: 0.5, defaultArea: 5, defaultRoute: 2 },
  maxSubcategories: 20,
} as const;

/** Dni tygodnia jako flagi – te same wartości co `WeekDays` w backendzie. */
export const WEEK_DAYS: readonly { bit: number; name: string; short: string }[] = [
  { bit: 1, name: 'Monday', short: 'Pn' },
  { bit: 2, name: 'Tuesday', short: 'Wt' },
  { bit: 4, name: 'Wednesday', short: 'Śr' },
  { bit: 8, name: 'Thursday', short: 'Cz' },
  { bit: 16, name: 'Friday', short: 'Pt' },
  { bit: 32, name: 'Saturday', short: 'So' },
  { bit: 64, name: 'Sunday', short: 'Nd' },
];

export const DAYS_WORKDAYS = 31;
export const DAYS_WEEKEND = 96;
export const DAYS_ALL = 127;

const NAMED_DAYS: Readonly<Record<string, number>> = {
  None: 0,
  ...Object.fromEntries(WEEK_DAYS.map((day) => [day.name, day.bit])),
  Workdays: DAYS_WORKDAYS,
  Weekend: DAYS_WEEKEND,
  All: DAYS_ALL,
};

/** Stan formularza alertu (ekran „Nowy alert” / edycja). Godziny jako „HH:mm” (pole `type="time"`). */
export interface AlertFormValue {
  name: string;
  kind: AlertKind;
  categoryId: string;
  /** Puste = cała kategoria. */
  subcategoryIds: string[];
  /** Obszar: środek. */
  center: GeoPoint | null;
  /** Trasa: start i cel (do wyznaczenia drogi) oraz gotowa polilinia. */
  routeStart: GeoPoint | null;
  routeEnd: GeoPoint | null;
  routePoints: GeoPoint[];
  radiusKm: number;
  /** Flagi dni (`WEEK_DAYS`). */
  days: number;
  /** `false` = całą dobę. */
  limitHours: boolean;
  from: string;
  to: string;
}

export type AlertFormField = 'name' | 'categoryId' | 'subcategoryIds' | 'place' | 'radiusKm' | 'days' | 'hours';
export type AlertFormErrors = Partial<Record<AlertFormField, string>>;

export function emptyAlertForm(kind: AlertKind): AlertFormValue {
  return {
    name: '',
    kind,
    categoryId: '',
    subcategoryIds: [],
    center: null,
    routeStart: null,
    routeEnd: null,
    routePoints: [],
    radiusKm: kind === 'Area' ? ALERT_RULES.radiusKm.defaultArea : ALERT_RULES.radiusKm.defaultRoute,
    days: DAYS_ALL,
    limitHours: false,
    from: '15:00',
    to: '18:00',
  };
}

export function alertFormFrom(alert: AlertDto): AlertFormValue {
  const route = alert.routePoints ?? [];
  const from = fromApiTime(alert.schedule?.from);
  const to = fromApiTime(alert.schedule?.to);
  return {
    name: alert.name,
    kind: alert.kind,
    categoryId: alert.categoryId,
    subcategoryIds: [...(alert.subcategoryIds ?? [])],
    center: alert.kind === 'Area' && alert.center ? toPoint(alert.center) : null,
    routeStart: route.length > 1 ? toPoint(route[0]) : null,
    routeEnd: route.length > 1 ? toPoint(route[route.length - 1]) : null,
    routePoints: route.map(toPoint),
    radiusKm: alert.radiusKm,
    days: parseWeekDays(alert.schedule?.days),
    limitHours: !!from && !!to,
    from: from ?? '15:00',
    to: to ?? '18:00',
  };
}

/** Błędy pól (puste = można zapisać). */
export function validateAlertForm(value: AlertFormValue): AlertFormErrors {
  const errors: AlertFormErrors = {};
  const name = value.name.trim();
  if (name.length === 0 || name.length > ALERT_RULES.name.max) {
    errors.name = `Podaj nazwę (do ${ALERT_RULES.name.max} znaków), np. „Dom” albo „Droga z pracy”.`;
  }
  if (!value.categoryId) {
    errors.categoryId = 'Wybierz kategorię.';
  }
  if (value.subcategoryIds.length > ALERT_RULES.maxSubcategories) {
    errors.subcategoryIds = `Najwyżej ${ALERT_RULES.maxSubcategories} podkategorii.`;
  }
  if (value.kind === 'Area' && !value.center) {
    errors.place = 'Wskaż miejsce na mapie, wyszukaj adres albo użyj swojej pozycji.';
  }
  if (value.kind === 'Route' && value.routePoints.length < 2) {
    errors.place = 'Wybierz start i cel, a potem wyznacz trasę.';
  }
  const { min, max } = ALERT_RULES.radiusKm;
  if (!Number.isFinite(value.radiusKm) || value.radiusKm < min || value.radiusKm > max) {
    errors.radiusKm = `Promień od ${min} do ${max} km.`;
  }
  if ((value.days & DAYS_ALL) === 0) {
    errors.days = 'Wybierz przynajmniej jeden dzień.';
  }
  if (value.limitHours) {
    if (!isTime(value.from) || !isTime(value.to)) {
      errors.hours = 'Podaj obie godziny.';
    } else if (value.from === value.to) {
      errors.hours = 'Godziny „od” i „do” muszą się różnić (bez godzin = całą dobę).';
    }
  }
  return errors;
}

export function toCreateAlert(value: AlertFormValue): CreateAlert {
  return {
    name: value.name.trim(),
    kind: value.kind,
    categoryId: value.categoryId,
    subcategoryIds: value.subcategoryIds,
    ...placeOf(value),
    radiusKm: value.radiusKm,
    schedule: scheduleOf(value),
  } as CreateAlert;
}

/** PATCH ze wszystkimi polami (formularz pokazuje całość); rodzaju się nie zmienia – backend go nie przyjmuje. */
export function toUpdateAlert(value: AlertFormValue): UpdateAlert {
  return {
    name: value.name.trim(),
    categoryId: value.categoryId,
    subcategoryIds: value.subcategoryIds,
    ...placeOf(value),
    radiusKm: value.radiusKm,
    schedule: scheduleOf(value),
  } as UpdateAlert;
}

/** Obszar → `center`, trasa → `routePoints` (drugie pole nie idzie – klient NSwag ma je jako wymagane, API jako opcjonalne). */
function placeOf(value: AlertFormValue): { center?: LocationDto; routePoints?: LocationDto[] } {
  return value.kind === 'Area' && value.center
    ? { center: toPoint(value.center) }
    : { routePoints: value.routePoints.map(toPoint) };
}

function scheduleOf(value: AlertFormValue): AlertScheduleDto {
  return {
    days: formatWeekDays(value.days),
    from: value.limitHours ? toApiTime(value.from) : undefined,
    to: value.limitHours ? toApiTime(value.to) : undefined,
  };
}

/** „Monday, Friday” / „Workdays” (flagi z backendu, `JsonStringEnumConverter`) → maska bitów. Brak = codziennie. */
export function parseWeekDays(value: string | undefined | null): number {
  if (!value) {
    return DAYS_ALL;
  }
  return value
    .split(',')
    .map((part) => NAMED_DAYS[part.trim()] ?? 0)
    .reduce((mask, bit) => mask | bit, 0);
}

/** Maska → wartość `WeekDays` dla API (pojedyncze dni rozdzielone przecinkiem – backend czyta flagi). */
export function formatWeekDays(mask: number): WeekDays {
  const days = mask & DAYS_ALL;
  if (days === DAYS_ALL) {
    return 'All';
  }
  if (days === DAYS_WORKDAYS) {
    return 'Workdays';
  }
  if (days === DAYS_WEEKEND) {
    return 'Weekend';
  }
  const names = WEEK_DAYS.filter((day) => (days & day.bit) !== 0).map((day) => day.name);
  return (names.length > 0 ? names.join(', ') : 'None') as WeekDays;
}

/** „Codziennie” / „Dni robocze” / „Weekend” / „Pn, Śr, Pt”. */
export function describeDays(mask: number): string {
  const days = mask & DAYS_ALL;
  if (days === DAYS_ALL) {
    return 'Codziennie';
  }
  if (days === DAYS_WORKDAYS) {
    return 'Dni robocze';
  }
  if (days === DAYS_WEEKEND) {
    return 'Weekend';
  }
  return WEEK_DAYS.filter((day) => (days & day.bit) !== 0)
    .map((day) => day.short)
    .join(', ');
}

/** Na liście: „Codziennie, całą dobę” / „Dni robocze, 15:00–18:00”. */
export function describeSchedule(schedule: AlertScheduleDto | undefined): string {
  const days = describeDays(parseWeekDays(schedule?.days));
  const from = fromApiTime(schedule?.from);
  const to = fromApiTime(schedule?.to);
  return from && to ? `${days}, ${from}–${to}` : `${days}, całą dobę`;
}

/** „15:00:00” (TimeOnly z API) → „15:00”. */
export function fromApiTime(value: string | undefined | null): string | undefined {
  const match = /^(\d{1,2}):(\d{2})/.exec(value ?? '');
  return match ? `${match[1].padStart(2, '0')}:${match[2]}` : undefined;
}

/** „15:00” → „15:00:00”. */
export function toApiTime(value: string): string {
  return `${fromApiTime(value) ?? value}:00`;
}

/** Promień na liście i przy suwaku: „5 km”, „0,5 km”. */
export function formatRadius(radiusKm: number): string {
  return `${radiusKm.toLocaleString('pl-PL', { maximumFractionDigits: 1 })} km`;
}

function isTime(value: string): boolean {
  return /^\d{2}:\d{2}$/.test(value);
}

function toPoint(point: GeoPoint): GeoPoint {
  return { latitude: point.latitude, longitude: point.longitude };
}
