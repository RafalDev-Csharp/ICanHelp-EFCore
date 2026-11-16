import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { IonButton } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { cloudOfflineOutline } from 'ionicons/icons';

import { EmptyStateComponent } from '../empty-state/empty-state.component';

/**
 * Stan `failed` ekranu albo listy: „Nie udało się wczytać …” z przyciskiem „Spróbuj ponownie” (`retry`).
 * Szczegóły błędu pokazał już interceptor (toast) – tu tylko droga wyjścia.
 */
@Component({
  selector: 'app-load-failed',
  template: `
    <app-empty-state
      icon="cloud-offline-outline"
      [heading]="heading()"
      message="Sprawdź połączenie z internetem i spróbuj ponownie."
    >
      <ion-button (click)="retry.emit()">Spróbuj ponownie</ion-button>
    </app-empty-state>
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
  imports: [EmptyStateComponent, IonButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadFailedComponent {
  /** Np. „Nie udało się wczytać rozmów”. */
  readonly heading = input.required<string>();
  readonly retry = output<void>();

  constructor() {
    addIcons({ cloudOfflineOutline });
  }
}
