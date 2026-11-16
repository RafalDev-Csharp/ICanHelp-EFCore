import { Component, Input, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonIcon,
  IonText,
  IonTextarea,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { star, starOutline } from 'ionicons/icons';

import type { RateAnnouncement } from '../../core/api/api-client';

import { OFFER_RULES } from './offer-form';

const RATES = [1, 2, 3, 4, 5] as const;

/** Okno oceny po zakończonym zleceniu: 1–5 gwiazdek + komentarz (opcjonalny, 2–500 znaków). */
@Component({
  selector: 'app-rating-modal',
  templateUrl: './rating-modal.component.html',
  styles: [
    `
      .stars {
        display: flex;
        justify-content: center;
        gap: 4px;
        margin: 8px 0 24px;
      }
      .stars ion-icon {
        font-size: 2.5rem;
      }
    `,
  ],
  imports: [
    FormsModule,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonIcon,
    IonText,
    IonTextarea,
    IonFooter,
  ],
})
export class RatingModalComponent {
  private readonly modal = inject(ModalController);

  /** Kogo oceniamy, np. „Jana” – do nagłówka „Jak oceniasz współpracę?”. */
  @Input() otherParty = '';

  protected readonly rates = RATES;
  protected readonly maxComment = OFFER_RULES.message.max;
  readonly rate = signal(0);
  comment = '';
  protected readonly submitted = signal(false);

  constructor() {
    addIcons({ star, starOutline });
  }

  cancel(): void {
    void this.modal.dismiss(null, 'cancel');
  }

  protected commentError(): string {
    const length = this.comment.trim().length;
    return length > 0 && length < OFFER_RULES.message.min ? 'Komentarz: co najmniej 2 znaki albo zostaw puste.' : '';
  }

  submit(): void {
    this.submitted.set(true);
    if (this.rate() === 0 || this.commentError()) {
      return;
    }
    const command: RateAnnouncement = { rate: this.rate(), comment: this.comment.trim() || undefined };
    void this.modal.dismiss(command, 'confirm');
  }
}
