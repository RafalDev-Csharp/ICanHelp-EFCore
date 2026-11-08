import { Component, input } from '@angular/core';
import { IonIcon } from '@ionic/angular';

/** Pusty stan ekranu: ikona, nagłówek, opis i opcjonalny przycisk (treść wstawiona do środka). */
@Component({
  selector: 'app-empty-state',
  templateUrl: './empty-state.component.html',
  styleUrls: ['./empty-state.component.scss'],
  imports: [IonIcon],
})
export class EmptyStateComponent {
  /** Nazwa ikony Ionicons zarejestrowanej przez `addIcons` (np. `megaphone-outline`). */
  readonly icon = input<string>();
  readonly heading = input.required<string>();
  readonly message = input<string>();
}
