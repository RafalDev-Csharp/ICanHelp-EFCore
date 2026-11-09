import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';

import { announcement } from '../testing/announcement-fixtures';

import { createAnnouncementForm, formValueOf, hasChanges, toCreateCommand, toUpdateCommand } from './announcement-form';

describe('announcement form', () => {
  const original = announcement('a1', {
    title: 'Cieknący kran',
    description: 'Kran w kuchni cieknie.',
    budget: { amount: 100, currency: 'PLN' },
    location: { latitude: 53.844, longitude: 22.979 },
    revision: 3,
  });

  function form(): ReturnType<typeof createAnnouncementForm> {
    return createAnnouncementForm(TestBed.inject(NonNullableFormBuilder));
  }

  it('validates like the backend', () => {
    const value = form();
    expect(value.valid).toBe(false);

    value.setValue({ ...formValueOf(original), title: '   ' });
    expect(value.controls.title.hasError('blank')).toBe(true);

    value.setValue({ ...formValueOf(original), budget: 0 });
    expect(value.controls.budget.hasError('min')).toBe(true);

    value.setValue({ ...formValueOf(original), budget: 99.999 });
    expect(value.controls.budget.hasError('cents')).toBe(true);

    value.setValue(formValueOf(original));
    expect(value.valid).toBe(true);
  });

  it('builds the create command with trimmed texts and PLN', () => {
    const command = toCreateCommand(
      { ...formValueOf(original), title: ' Kran ', description: ' Cieknie. ', budget: 120.5 },
      { latitude: 53.8, longitude: 22.9 },
    );

    expect(command).toEqual({
      categoryId: 'cat-home',
      subcategoryId: 'sub-plumbing',
      difficulty: 'Easy',
      title: 'Kran',
      description: 'Cieknie.',
      budget: { amount: 120.5, currency: 'PLN' },
      location: { latitude: 53.8, longitude: 22.9 },
    });
  });

  it('sends only changed fields with the edited revision', () => {
    const unchanged = toUpdateCommand(original, formValueOf(original), original.location);
    expect(unchanged).toEqual({ expectedRevision: 3 });
    expect(hasChanges(unchanged)).toBe(false);

    const patch = toUpdateCommand(
      original,
      { ...formValueOf(original), subcategoryId: 'sub-other', budget: 150, title: 'Cieknący kran ' },
      { latitude: 53.85, longitude: 22.979 },
    );
    expect(patch).toEqual({
      expectedRevision: 3,
      categoryId: 'cat-home',
      subcategoryId: 'sub-other',
      budget: { amount: 150, currency: 'PLN' },
      location: { latitude: 53.85, longitude: 22.979 },
    });
    expect(hasChanges(patch)).toBe(true);
  });
});
