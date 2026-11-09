import type { OnInit } from '@angular/core';
import { Component, Input, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonIcon,
  IonInput,
  IonText,
  IonTextarea,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { warningOutline } from 'ionicons/icons';

import type { OfferDto } from '../../core/api/api-client';

import type { OfferForm } from './offer-form';
import { OFFER_RULES, createOfferForm, toOfferCommand } from './offer-form';

/**
 * Okno oferty / kontroferty (Ionic modal): cena, termin od–do, wiadomość. Zamyka się z `MakeOffer` (rola `confirm`)
 * albo bez danych (`cancel`). Otwiera je `OffersService` – właściwości ustawia `componentProps`.
 */
@Component({
  selector: 'app-offer-form-modal',
  templateUrl: './offer-form-modal.component.html',
  styles: [
    `
      .offer-fields {
        display: flex;
        flex-direction: column;
        gap: 16px;
      }
      .offer-note {
        display: flex;
        gap: 8px;
        align-items: flex-start;
        margin: 0 0 16px;
      }
      .offer-error {
        margin: -8px 0 0;
        font-size: 0.8125rem;
      }
    `,
  ],
  imports: [
    ReactiveFormsModule,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonInput,
    IonTextarea,
    IonText,
    IonIcon,
    IonFooter,
  ],
})
export class OfferFormModalComponent implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly modal = inject(ModalController);

  /** Nagłówek okna, np. „Twoja oferta” / „Kontroferta”. */
  @Input() heading = 'Twoja oferta';
  /** Oferta, na którą odpowiadamy – podpowiada cenę i termin. */
  @Input() basis?: OfferDto;
  /** Podpowiedź ceny przy pierwszej ofercie (budżet ogłoszenia). */
  @Input() suggestedPrice?: number;
  /** Ostrzeżenie nad formularzem (np. „ogłoszenie zmieniło się od tej oferty: budżet”). */
  @Input() note?: string;

  protected readonly rules = OFFER_RULES;
  protected form!: OfferForm;
  protected readonly submitted = signal(false);

  constructor() {
    addIcons({ warningOutline });
  }

  ngOnInit(): void {
    this.form = createOfferForm(this.fb, { basis: this.basis, suggestedPrice: this.suggestedPrice });
  }

  cancel(): void {
    void this.modal.dismiss(null, 'cancel');
  }

  submit(): void {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    void this.modal.dismiss(toOfferCommand(this.form.getRawValue()), 'confirm');
  }

  /** Błąd terminu (walidator grupy) – pokazujemy pod polami dat po pierwszej próbie wysłania. */
  protected datesError(): string {
    if (!this.submitted()) {
      return '';
    }
    if (this.form.hasError('startInPast')) {
      return 'Początek nie może być w przeszłości.';
    }
    if (this.form.hasError('endBeforeStart')) {
      return 'Koniec musi być później niż początek.';
    }
    return this.form.hasError('dates') ? 'Podaj początek i koniec.' : '';
  }
}
