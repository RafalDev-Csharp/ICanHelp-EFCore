import type { Type } from '@angular/core';
import { Injectable, inject } from '@angular/core';
import { ModalController } from '@ionic/angular';

/**
 * Okna z własnym komponentem (Ionic modal – formularz oferty, ocena, powód rezygnacji). Komponent okna zamyka się
 * przez `ModalController.dismiss(dane, 'confirm')` albo bez danych (`cancel`); wołający dostaje gotowy wynik.
 */
@Injectable({ providedIn: 'root' })
export class ModalService {
  private readonly modals = inject(ModalController);

  /**
   * Otwiera okno; `props` trafiają do `@Input()` komponentu. Wynik: dane, z którymi okno zamknięto rolą `confirm`,
   * albo `null` (anulowane).
   */
  async open<T>(component: Type<unknown>, props: object = {}): Promise<T | null> {
    const modal = await this.modals.create({ component, componentProps: { ...props } });
    await modal.present();
    const { data, role } = await modal.onWillDismiss<T>();
    return role === 'confirm' && data ? data : null;
  }
}
