import { Injectable, inject } from '@angular/core';
import { AlertController } from '@ionic/angular';

import { ToastService } from './toast.service';

/** Pytanie „czy na pewno?” (Ionic alert z dwoma przyciskami). */
export interface ConfirmOptions {
  header: string;
  message: string;
  /** Przycisk potwierdzenia, np. „Przyjmij”, „Usuń”. */
  confirmText: string;
  /** Przycisk rezygnacji – domyślnie „Nie”. */
  cancelText?: string;
  /** Akcja nie do cofnięcia – przycisk potwierdzenia wyróżniony jako niebezpieczny. */
  destructive?: boolean;
}

/** Okno z jednym polem tekstowym (Ionic alert z `textarea`). */
export interface PromptOptions {
  header: string;
  message: string;
  placeholder: string;
  /** Przycisk wysłania, np. „Wyślij”. */
  confirmText: string;
  minLength: number;
  maxLength: number;
  /** Komunikat, gdy tekst jest za krótki – okno zostaje otwarte. */
  tooShortMessage: string;
}

/**
 * Proste okna wspólne dla całej aplikacji: potwierdzenie i pole tekstowe (Ionic alert). Ekrany i serwisy dostają
 * gotową odpowiedź (`true` / tekst), bez powtarzania `create` → `present` → `onDidDismiss`. Okna z własnym
 * komponentem (formularz oferty, ocena) – `ModalService`.
 */
@Injectable({ providedIn: 'root' })
export class DialogService {
  private readonly alerts = inject(AlertController);
  private readonly toast = inject(ToastService);

  /** `true`, gdy użytkownik potwierdził. */
  async confirm(options: ConfirmOptions): Promise<boolean> {
    const alert = await this.alerts.create({
      header: options.header,
      message: options.message,
      buttons: [
        { text: options.cancelText ?? 'Nie', role: 'cancel' },
        { text: options.confirmText, role: options.destructive ? 'destructive' : 'confirm' },
      ],
    });
    await alert.present();
    const { role } = await alert.onDidDismiss();
    return role === 'confirm' || role === 'destructive';
  }

  /** Tekst wpisany przez użytkownika (bez spacji na końcach) albo `null`, gdy zamknął okno. */
  async prompt(options: PromptOptions): Promise<string | null> {
    const alert = await this.alerts.create({
      header: options.header,
      message: options.message,
      inputs: [
        {
          name: 'text',
          type: 'textarea',
          placeholder: options.placeholder,
          attributes: { maxlength: options.maxLength },
        },
      ],
      buttons: [
        { text: 'Anuluj', role: 'cancel' },
        {
          text: options.confirmText,
          role: 'confirm',
          handler: (values: { text?: string }) => {
            if ((values.text ?? '').trim().length < options.minLength) {
              void this.toast.error(options.tooShortMessage);
              return false;
            }
            return true;
          },
        },
      ],
    });
    await alert.present();
    const { data, role } = await alert.onDidDismiss<{ values?: { text?: string } }>();
    const text = data?.values?.text?.trim() ?? '';
    return role === 'confirm' && text.length >= options.minLength ? text : null;
  }
}
