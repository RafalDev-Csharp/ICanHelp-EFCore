import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';

import type { SignUpFormValue } from './sign-up-form';
import { createSignUpForm, normalizePhoneNumber, toSignUpCommand } from './sign-up-form';

describe('sign-up form', () => {
  const valid: SignUpFormValue = {
    userName: 'jan.kowalski',
    fullName: 'Jan Kowalski',
    email: 'jan@example.com',
    password: 'Haslo123!',
    phoneNumber: '+48600100200',
    country: 'Polska',
    city: 'Augustów',
    postalCode: '16-300',
    description: '',
  };

  it('starts with Poland as the country and accepts complete data', () => {
    const form = createSignUpForm(TestBed.inject(NonNullableFormBuilder));
    expect(form.controls.country.value).toBe('Polska');
    expect(form.valid).toBe(false);

    form.setValue(valid);

    expect(form.valid).toBe(true);
  });

  it('removes spaces and dashes from a phone number', () => {
    expect(normalizePhoneNumber('+48 600-100-200')).toBe('+48600100200');
    expect(normalizePhoneNumber('600100200')).toBe('600100200');
  });

  it('builds the command from trimmed texts, without an empty description', () => {
    const command = toSignUpCommand({
      ...valid,
      userName: ' jan.kowalski ',
      fullName: ' Jan Kowalski ',
      email: ' jan@example.com ',
      phoneNumber: '+48 600 100 200',
      city: ' Augustów ',
      description: '   ',
    });

    expect(command).toEqual({ ...valid, description: undefined });
    // Hasła nie obcinamy – spacja może być jego częścią.
    expect(toSignUpCommand({ ...valid, password: ' tajne ' }).password).toBe(' tajne ');
    expect(toSignUpCommand({ ...valid, description: ' Złota rączka. ' }).description).toBe('Złota rączka.');
  });
});
