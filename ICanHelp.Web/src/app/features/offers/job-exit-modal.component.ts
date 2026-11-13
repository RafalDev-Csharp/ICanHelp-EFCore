import { Component, Input, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonItem,
  IonList,
  IonRadio,
  IonRadioGroup,
  IonText,
  IonTextarea,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';

import type { JobExitReason } from '../../core/api/api-client';

import type { JobExitAction, JobExitCommand, JobExitReasonOption } from './job-exit';
import { jobExitReasons } from './job-exit';
import { OFFER_RULES } from './offer-form';

/**
 * F11 E: okno „Zrezygnuj” / „Przerwij zlecenie” – obowiązkowy powód z listy i opis (wymagany przy „Inny”,
 * 2–500 znaków). Wynik: `JobExitCommand` z rolą `confirm`.
 */
@Component({
  selector: 'app-job-exit-modal',
  templateUrl: './job-exit-modal.component.html',
  imports: [
    FormsModule,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonButton,
    IonTitle,
    IonContent,
    IonList,
    IonItem,
    IonRadioGroup,
    IonRadio,
    IonText,
    IonTextarea,
    IonFooter,
  ],
})
export class JobExitModalComponent {
  private readonly modal = inject(ModalController);

  @Input() action: JobExitAction = 'abandon';
  /** Kto jest drugą stroną – do tekstu „… dostanie powiadomienie z powodem”. */
  @Input() otherSide = 'Druga strona';

  protected readonly maxNote = OFFER_RULES.message.max;
  readonly reason = signal<JobExitReason | null>(null);
  note = '';
  protected readonly submitted = signal(false);

  protected get reasons(): JobExitReasonOption[] {
    return jobExitReasons(this.action);
  }

  protected get heading(): string {
    return this.action === 'abandon' ? 'Rezygnacja' : 'Przerwanie zlecenia';
  }

  protected get intro(): string {
    return this.action === 'abandon'
      ? `Ogłoszenie wróci do ofert, a ${this.otherSide.toLowerCase()} dostanie powiadomienie z powodem. Rezygnacja jest widoczna w Twoim profilu. Bez ocen.`
      : `Zlecenie zostanie zakończone jako przerwane, rozmowa – zamknięta. Możecie się wzajemnie ocenić. Ustalcie między sobą rozliczenie za wykonaną część. Przerwanie jest widoczne w Twoim profilu.`;
  }

  protected get submitLabel(): string {
    return this.action === 'abandon' ? 'Zrezygnuj' : 'Przerwij zlecenie';
  }

  cancel(): void {
    void this.modal.dismiss(null, 'cancel');
  }

  protected noteError(): string {
    const length = this.note.trim().length;
    if (this.reason() === 'Other' && length === 0) {
      return 'Opisz powód.';
    }
    return length > 0 && length < OFFER_RULES.message.min ? 'Opis: co najmniej 2 znaki albo zostaw puste.' : '';
  }

  submit(): void {
    this.submitted.set(true);
    const reason = this.reason();
    if (!reason || this.noteError()) {
      return;
    }
    const command: JobExitCommand = { reason, note: this.note.trim() || undefined };
    void this.modal.dismiss(command, 'confirm');
  }
}
