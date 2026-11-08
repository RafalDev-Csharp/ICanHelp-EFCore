import { Component, inject, input, signal } from '@angular/core';
import type { AbstractControl, ValidationErrors } from '@angular/forms';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInput,
  IonInputPasswordToggle,
  IonSpinner,
  IonText,
  IonTextarea,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';

import { safeReturnUrl } from '../../core/auth/auth.guard';
import { AuthService } from '../../core/auth/auth.service';

/** Te same reguły co w backendzie (value objects w `ICanHelp.Core/ValueObjects`) – błąd widać przed wysłaniem. */
export const SIGN_UP_RULES = {
  userName: { min: 3, max: 30 },
  fullName: { min: 3, max: 100 },
  email: { max: 100 },
  password: { min: 6, max: 200 },
  /** Opcjonalny `+`, potem 8–15 cyfr (E.164) – jak `PhoneNumber`. */
  phoneNumber: /^\+?[1-9][0-9]{7,14}$/,
  text: { max: 100 },
  description: { min: 2, max: 500 },
} as const;

/** Pole nie może być samymi spacjami (backend: `IsNullOrWhiteSpace`). */
function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value && !control.value.trim() ? { blank: true } : null;
}

/** Opis jest opcjonalny, ale jeśli podany – co najmniej 2 znaki. */
function optionalDescription(control: AbstractControl<string>): ValidationErrors | null {
  const length = control.value.trim().length;
  return length > 0 && length < SIGN_UP_RULES.description.min ? { minlength: true } : null;
}

@Component({
  selector: 'app-register',
  templateUrl: './register.page.html',
  styleUrls: ['./auth.page.scss'],
  imports: [
    ReactiveFormsModule,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonInput,
    IonInputPasswordToggle,
    IonTextarea,
    IonButton,
    IonSpinner,
    IonText,
  ],
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>();
  readonly submitting = signal(false);
  readonly rules = SIGN_UP_RULES;

  readonly form = inject(NonNullableFormBuilder).group({
    userName: [
      '',
      [
        Validators.required,
        notBlank,
        Validators.minLength(SIGN_UP_RULES.userName.min),
        Validators.maxLength(SIGN_UP_RULES.userName.max),
      ],
    ],
    fullName: [
      '',
      [
        Validators.required,
        notBlank,
        Validators.minLength(SIGN_UP_RULES.fullName.min),
        Validators.maxLength(SIGN_UP_RULES.fullName.max),
      ],
    ],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(SIGN_UP_RULES.email.max)]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(SIGN_UP_RULES.password.min),
        Validators.maxLength(SIGN_UP_RULES.password.max),
      ],
    ],
    phoneNumber: ['', [Validators.required, Validators.pattern(SIGN_UP_RULES.phoneNumber)]],
    country: ['Polska', [Validators.required, notBlank, Validators.maxLength(SIGN_UP_RULES.text.max)]],
    city: ['', [Validators.required, notBlank, Validators.maxLength(SIGN_UP_RULES.text.max)]],
    postalCode: ['', [Validators.required, notBlank, Validators.maxLength(SIGN_UP_RULES.text.max)]],
    description: ['', [optionalDescription, Validators.maxLength(SIGN_UP_RULES.description.max)]],
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const description = value.description.trim();
    this.submitting.set(true);
    // Rejestracja + od razu logowanie. Błędy (zajęty e-mail, zła nazwa…) pokazuje interceptor jako toast.
    this.auth
      .signUp({
        userName: value.userName.trim(),
        fullName: value.fullName.trim(),
        email: value.email.trim(),
        password: value.password,
        // Spacje i myślniki z „600 100 200” wycina już pole; tu na wszelki wypadek.
        phoneNumber: value.phoneNumber.replace(/[\s-]/g, ''),
        country: value.country.trim(),
        city: value.city.trim(),
        postalCode: value.postalCode.trim(),
        description: description || undefined,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.form.reset();
          void this.router.navigateByUrl(safeReturnUrl(this.returnUrl()), { replaceUrl: true });
        },
        error: () => this.submitting.set(false),
      });
  }

  /** Numer bez spacji i myślników – walidacja i API dostają same cyfry. */
  normalizePhone(): void {
    const control = this.form.controls.phoneNumber;
    const normalized = control.value.replace(/[\s-]/g, '');
    if (normalized !== control.value) {
      control.setValue(normalized);
    }
  }
}
