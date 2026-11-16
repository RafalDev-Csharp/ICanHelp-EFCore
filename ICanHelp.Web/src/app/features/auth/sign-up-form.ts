import type { FormControl, FormGroup, NonNullableFormBuilder } from '@angular/forms';
import { Validators } from '@angular/forms';

import type { SignUp } from '../../core/api/api-client';
import { minLengthIfPresent, notBlank } from '../../shared/forms/validators';

/** Te same reguły co w backendzie (value objects w `ICanHelp.Core/ValueObjects`) – błąd widać przed wysłaniem. */
export const SIGN_UP_RULES = {
  userName: { min: 3, max: 30 },
  fullName: { min: 3, max: 100 },
  email: { max: 100 },
  password: { min: 6, max: 200 },
  /** Opcjonalny `+`, potem 8–15 cyfr (E.164) – jak `PhoneNumber`. */
  phoneNumber: /^\+?[1-9][0-9]{7,14}$/,
  text: { max: 100 },
  /** Opis jest opcjonalny, ale jeśli podany – co najmniej 2 znaki. */
  description: { min: 2, max: 500 },
} as const;

export type SignUpForm = FormGroup<{
  userName: FormControl<string>;
  fullName: FormControl<string>;
  email: FormControl<string>;
  password: FormControl<string>;
  phoneNumber: FormControl<string>;
  country: FormControl<string>;
  city: FormControl<string>;
  postalCode: FormControl<string>;
  description: FormControl<string>;
}>;

export type SignUpFormValue = ReturnType<SignUpForm['getRawValue']>;

export function createSignUpForm(fb: NonNullableFormBuilder): SignUpForm {
  const { userName, fullName, email, password, phoneNumber, text, description } = SIGN_UP_RULES;
  const requiredText = [Validators.required, notBlank, Validators.maxLength(text.max)];
  return fb.group({
    userName: [
      '',
      [Validators.required, notBlank, Validators.minLength(userName.min), Validators.maxLength(userName.max)],
    ],
    fullName: [
      '',
      [Validators.required, notBlank, Validators.minLength(fullName.min), Validators.maxLength(fullName.max)],
    ],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(email.max)]],
    password: ['', [Validators.required, Validators.minLength(password.min), Validators.maxLength(password.max)]],
    phoneNumber: ['', [Validators.required, Validators.pattern(phoneNumber)]],
    country: ['Polska', requiredText],
    city: ['', requiredText],
    postalCode: ['', requiredText],
    description: ['', [minLengthIfPresent(description.min), Validators.maxLength(description.max)]],
  });
}

/** „+48 600-100-200” → „+48600100200”: walidacja i API dostają numer bez spacji i myślników. */
export function normalizePhoneNumber(value: string): string {
  return value.replace(/[\s-]/g, '');
}

/** Ciało `POST api/users`: teksty bez spacji na końcach, pusty opis pominięty. */
export function toSignUpCommand(value: SignUpFormValue): SignUp {
  const description = value.description.trim();
  return {
    userName: value.userName.trim(),
    fullName: value.fullName.trim(),
    email: value.email.trim(),
    password: value.password,
    // Spacje i myślniki z „600 100 200” wycina już pole; tu na wszelki wypadek.
    phoneNumber: normalizePhoneNumber(value.phoneNumber),
    country: value.country.trim(),
    city: value.city.trim(),
    postalCode: value.postalCode.trim(),
    description: description || undefined,
  };
}
