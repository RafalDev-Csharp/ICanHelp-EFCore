import { ChangeDetectionStrategy, Component } from '@angular/core';
import { IonSpinner } from '@ionic/angular';

/** Spinner na czas pierwszego wczytania ekranu albo listy (stan `loading`). */
@Component({
  selector: 'app-loading',
  template: `
    <div class="ion-text-center ion-padding" aria-busy="true">
      <ion-spinner name="crescent"></ion-spinner>
    </div>
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
  imports: [IonSpinner],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadingComponent {}
