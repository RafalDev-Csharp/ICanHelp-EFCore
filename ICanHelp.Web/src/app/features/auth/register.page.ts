import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
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
import { BrandComponent } from '../../shared/components/brand/brand.component';

import { SIGN_UP_RULES, createSignUpForm, normalizePhoneNumber, toSignUpCommand } from './sign-up-form';

/**
 * Rejestracja (`/register`, poza zakładkami; `?returnUrl=` – dokąd wrócić): formularz z regułami backendu
 * (`sign-up-form.ts`) → `POST api/users` i od razu logowanie.
 */
@Component({
  selector: 'app-register',
  templateUrl: './register.page.html',
  styleUrls: ['./auth.page.scss'],
  imports: [
    BrandComponent,
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

  readonly form = createSignUpForm(inject(NonNullableFormBuilder));

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    // Rejestracja + od razu logowanie. Błędy (zajęty e-mail, zła nazwa…) pokazuje interceptor jako toast.
    this.auth.signUp(toSignUpCommand(this.form.getRawValue())).subscribe({
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
    const normalized = normalizePhoneNumber(control.value);
    if (normalized !== control.value) {
      control.setValue(normalized);
    }
  }
}
