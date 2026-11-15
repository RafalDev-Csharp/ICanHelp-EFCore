import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Pole nie może być samymi spacjami (backend: `IsNullOrWhiteSpace`). Błąd: `blank`. */
export function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value && !control.value.trim() ? { blank: true } : null;
}

/** Kwota: najwyżej 2 miejsca po przecinku (grosze). Błąd: `cents`. */
export function cents(control: AbstractControl<number | null>): ValidationErrors | null {
  const value = control.value;
  return value !== null && Math.round(value * 100) !== value * 100 ? { cents: true } : null;
}

/**
 * Tekst opcjonalny, ale jeśli podany (po obcięciu spacji) – co najmniej `min` znaków (backend: `Description`).
 * Błąd: `minlength`.
 */
export function minLengthIfPresent(min: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const length = String(control.value ?? '').trim().length;
    return length > 0 && length < min ? { minlength: true } : null;
  };
}
