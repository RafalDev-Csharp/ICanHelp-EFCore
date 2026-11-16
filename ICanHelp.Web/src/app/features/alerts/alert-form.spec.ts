import type { AlertDto } from '../../core/api/api-client';

import type { AlertFormValue } from './alert-form';
import {
  DAYS_ALL,
  DAYS_WEEKEND,
  DAYS_WORKDAYS,
  alertFormFrom,
  describeSchedule,
  emptyAlertForm,
  formatRadius,
  formatWeekDays,
  fromApiTime,
  parseWeekDays,
  toApiTime,
  toCreateAlert,
  toUpdateAlert,
  validateAlertForm,
} from './alert-form';

function areaForm(changes: Partial<AlertFormValue> = {}): AlertFormValue {
  return {
    ...emptyAlertForm('Area'),
    name: 'Dom',
    categoryId: 'cat-home',
    center: { latitude: 53.844, longitude: 22.979 },
    ...changes,
  };
}

const routeAlert: AlertDto = {
  id: 'al-2',
  name: 'Suwałki → Augustów',
  kind: 'Route',
  categoryId: 'cat-home',
  subcategoryIds: ['sub-plumbing'],
  center: undefined as unknown as AlertDto['center'],
  routePoints: [
    { latitude: 54.1, longitude: 22.93 },
    { latitude: 53.97, longitude: 22.95 },
    { latitude: 53.84, longitude: 22.98 },
  ],
  radiusKm: 2,
  schedule: { days: 'Workdays', from: '15:00:00', to: '18:00:00' },
  isEnabled: true,
  createdAt: '2026-11-10T10:00:00',
  updatedAt: undefined,
};

describe('alert form', () => {
  describe('week days', () => {
    it('reads flags the way the backend writes them', () => {
      expect(parseWeekDays('All')).toBe(DAYS_ALL);
      expect(parseWeekDays('Workdays')).toBe(DAYS_WORKDAYS);
      expect(parseWeekDays('Monday, Friday')).toBe(1 | 16);
      expect(parseWeekDays('Saturday, Workdays')).toBe(DAYS_ALL & ~64);
      expect(parseWeekDays(undefined)).toBe(DAYS_ALL);
    });

    it('writes named groups or single days separated by commas', () => {
      expect(formatWeekDays(DAYS_ALL)).toBe('All');
      expect(formatWeekDays(DAYS_WORKDAYS)).toBe('Workdays');
      expect(formatWeekDays(DAYS_WEEKEND)).toBe('Weekend');
      expect(formatWeekDays(1 | 4 | 16)).toBe('Monday, Wednesday, Friday');
      expect(formatWeekDays(0)).toBe('None');
    });

    it('describes the schedule in Polish', () => {
      expect(describeSchedule({ days: 'All', from: undefined, to: undefined })).toBe('Codziennie, całą dobę');
      expect(describeSchedule({ days: 'Workdays', from: '15:00:00', to: '18:00:00' })).toBe('Dni robocze, 15:00–18:00');
      expect(describeSchedule({ days: 'Monday, Friday' as never, from: undefined, to: undefined })).toBe(
        'Pn, Pt, całą dobę',
      );
    });
  });

  it('converts times between the time field and TimeOnly', () => {
    expect(fromApiTime('15:00:00')).toBe('15:00');
    expect(fromApiTime('7:05:00')).toBe('07:05');
    expect(fromApiTime(undefined)).toBeUndefined();
    expect(toApiTime('22:30')).toBe('22:30:00');
  });

  it('formats the radius with a Polish decimal comma', () => {
    expect(formatRadius(0.5)).toBe('0,5 km');
    expect(formatRadius(5)).toBe('5 km');
  });

  describe('validation', () => {
    it('accepts a complete area alert', () => {
      expect(validateAlertForm(areaForm())).toEqual({});
    });

    it('requires a name, a category and a place', () => {
      const errors = validateAlertForm({ ...emptyAlertForm('Area'), name: '   ' });

      expect(Object.keys(errors).sort()).toEqual(['categoryId', 'name', 'place']);
    });

    it('requires a planned route for a route alert', () => {
      const route = { ...emptyAlertForm('Route'), name: 'Praca', categoryId: 'cat-home' };

      expect(validateAlertForm(route).place).toContain('wyznacz trasę');
      expect(
        validateAlertForm({
          ...route,
          routePoints: [
            { latitude: 54.1, longitude: 22.93 },
            { latitude: 53.84, longitude: 22.98 },
          ],
        }),
      ).toEqual({});
    });

    it('checks the radius, the days and the hours', () => {
      expect(validateAlertForm(areaForm({ radiusKm: 0.2 })).radiusKm).toBeDefined();
      expect(validateAlertForm(areaForm({ radiusKm: 51 })).radiusKm).toBeDefined();
      expect(validateAlertForm(areaForm({ days: 0 })).days).toBeDefined();
      expect(validateAlertForm(areaForm({ limitHours: true, from: '10:00', to: '10:00' })).hours).toBeDefined();
      expect(validateAlertForm(areaForm({ limitHours: true, from: '', to: '06:00' })).hours).toBeDefined();
      // Przez noc – poprawne.
      expect(validateAlertForm(areaForm({ limitHours: true, from: '22:00', to: '06:00' }))).toEqual({});
    });
  });

  describe('requests', () => {
    it('sends an area alert with the centre and without hours', () => {
      expect(
        toCreateAlert(areaForm({ name: '  Dom ', subcategoryIds: ['sub-plumbing'], days: DAYS_WORKDAYS })),
      ).toEqual({
        name: 'Dom',
        kind: 'Area',
        categoryId: 'cat-home',
        subcategoryIds: ['sub-plumbing'],
        center: { latitude: 53.844, longitude: 22.979 },
        radiusKm: 5,
        schedule: { days: 'Workdays', from: undefined, to: undefined },
      });
    });

    it('sends a route alert with the polyline and the hours', () => {
      const form = alertFormFrom(routeAlert);

      const body = toCreateAlert(form);

      expect(body.kind).toBe('Route');
      expect(body.routePoints).toEqual(routeAlert.routePoints);
      expect('center' in body).toBe(false);
      expect(body.schedule).toEqual({ days: 'Workdays', from: '15:00:00', to: '18:00:00' });
    });

    it('updates everything except the kind', () => {
      const body = toUpdateAlert(areaForm({ radiusKm: 10 }));

      expect('kind' in body).toBe(false);
      expect(body.radiusKm).toBe(10);
      expect(body.center).toEqual({ latitude: 53.844, longitude: 22.979 });
      expect('routePoints' in body).toBe(false);
    });
  });

  it('fills the form from a saved route alert', () => {
    const form = alertFormFrom(routeAlert);

    expect(form.routeStart).toEqual({ latitude: 54.1, longitude: 22.93 });
    expect(form.routeEnd).toEqual({ latitude: 53.84, longitude: 22.98 });
    expect(form.routePoints).toHaveLength(3);
    expect(form.center).toBeNull();
    expect(form.days).toBe(DAYS_WORKDAYS);
    expect(form).toMatchObject({ limitHours: true, from: '15:00', to: '18:00', subcategoryIds: ['sub-plumbing'] });
  });
});
