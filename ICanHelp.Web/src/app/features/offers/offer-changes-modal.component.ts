import { Component, Input, inject } from '@angular/core';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonItem,
  IonLabel,
  IonList,
  IonTitle,
  IonToolbar,
  ModalController,
} from '@ionic/angular';

import type { FieldChangeRow } from './offer-changes';

/** Wynik okna: co autor nieaktualnej oferty wybrał po obejrzeniu zmian (`cancel` – zamknął okno). */
export type OfferChangesChoice = 'keep' | 'change' | 'withdraw';

/**
 * Podgląd zmian ogłoszenia (było → jest). Z `withActions` – dla autora oferty sprzed edycji: „Podtrzymuję ofertę”
 * (te same warunki do nowej wersji), „Zmień ofertę” (formularz), „Wycofaj”. Bez – sam podgląd (wpis w wątku).
 */
@Component({
  selector: 'app-offer-changes-modal',
  templateUrl: './offer-changes-modal.component.html',
  styles: [
    `
      .change-before {
        text-decoration: line-through;
        color: var(--ion-color-medium);
      }
      .change-after {
        font-weight: 600;
        color: var(--ion-text-color);
      }
      .intro {
        margin: 16px 16px 0;
      }
      .choices {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }
    `,
  ],
  imports: [IonHeader, IonToolbar, IonButtons, IonButton, IonTitle, IonContent, IonList, IonItem, IonLabel, IonFooter],
})
export class OfferChangesModalComponent {
  private readonly modal = inject(ModalController);

  @Input() heading = 'Zmiany w ogłoszeniu';
  /** Zdanie nad listą, np. „Zleceniodawca zmienił ogłoszenie po Twojej ofercie.” */
  @Input() intro = '';
  @Input() rows: FieldChangeRow[] = [];
  /** Przyciski decyzji autora oferty (podtrzymaj / zmień / wycofaj). */
  @Input() withActions = false;

  close(): void {
    void this.modal.dismiss(null, 'cancel');
  }

  choose(choice: OfferChangesChoice): void {
    void this.modal.dismiss(choice, 'confirm');
  }
}
