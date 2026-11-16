import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';

import { createOfferForm, defaultTerm, fromLocalInput, toLocalInput, toOfferCommand } from './offer-form';
import { offerFixture } from './testing/offer-fixtures';

describe('offer form', () => {
  const now = new Date(2026, 10, 9, 14, 20);
  const fb = (): NonNullableFormBuilder => TestBed.inject(NonNullableFormBuilder);

  it('suggests the next full hour at least an hour away, for 2 hours', () => {
    const { start, end } = defaultTerm(now);
    expect(start).toEqual(new Date(2026, 10, 9, 16, 0));
    expect(end).toEqual(new Date(2026, 10, 9, 18, 0));
  });

  it('round-trips local datetime input values', () => {
    expect(toLocalInput(new Date(2026, 0, 5, 7, 3))).toBe('2026-01-05T07:03');
    expect(fromLocalInput('2026-01-05T07:03')).toEqual(new Date(2026, 0, 5, 7, 3));
    expect(fromLocalInput('')).toBeNull();
  });

  it('starts with the budget as the price and is valid', () => {
    const form = createOfferForm(fb(), { suggestedPrice: 150, now: () => now });
    expect(form.controls.price.value).toBe(150);
    expect(form.valid).toBe(true);
  });

  it('copies price and a future term from the offer it answers', () => {
    const basis = offerFixture('o1', {
      price: { amount: 99.5, currency: 'PLN' },
      proposedStart: new Date(2026, 10, 20, 10, 0).toISOString(),
      proposedEnd: new Date(2026, 10, 20, 12, 0).toISOString(),
    });
    const form = createOfferForm(fb(), { basis, now: () => now });
    expect(form.controls.price.value).toBe(99.5);
    expect(form.controls.start.value).toBe('2026-11-20T10:00');
  });

  it('rejects a start in the past, an end before the start and bad prices', () => {
    const form = createOfferForm(fb(), { suggestedPrice: 100, now: () => now });

    form.controls.start.setValue('2026-11-09T10:00');
    expect(form.hasError('startInPast')).toBe(true);

    form.controls.start.setValue('2026-11-10T10:00');
    form.controls.end.setValue('2026-11-10T09:00');
    expect(form.hasError('endBeforeStart')).toBe(true);

    form.controls.end.setValue('2026-11-10T11:00');
    expect(form.valid).toBe(true);

    form.controls.price.setValue(10.555);
    expect(form.controls.price.hasError('cents')).toBe(true);
    form.controls.price.setValue(0);
    expect(form.controls.price.hasError('min')).toBe(true);

    form.controls.price.setValue(10);
    form.controls.message.setValue(' a ');
    expect(form.controls.message.hasError('minlength')).toBe(true);
  });

  it('builds the API command with UTC dates and a trimmed message', () => {
    const command = toOfferCommand({
      price: 80,
      start: '2026-11-10T10:00',
      end: '2026-11-10T12:00',
      message: '  Mam narzędzia.  ',
    });
    expect(command).toEqual({
      price: { amount: 80, currency: 'PLN' },
      proposedStart: new Date(2026, 10, 10, 10, 0).toISOString(),
      proposedEnd: new Date(2026, 10, 10, 12, 0).toISOString(),
      message: 'Mam narzędzia.',
    });
    expect(
      toOfferCommand({ price: 80, start: '2026-11-10T10:00', end: '2026-11-10T12:00', message: ' ' }).message,
    ).toBeUndefined();
  });
});
