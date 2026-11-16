import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { IonButton, NavController } from '@ionic/angular';

/** Strona główna aplikacji – tam prowadzi logo. */
export const HOME_URL = '/announcements';

/**
 * Logo (uścisk dłoni na pomarańczowym kafelku, `icons/logo.svg`) i nazwa „I can help U” w trzech liniach (I / can help / U) w nagłówku
 * (wewnątrz `ion-buttons`) → strona główna (lista ogłoszeń). `navigateRoot` czyści stos zakładki Ogłoszenia, więc
 * ze szczegółów wracamy do listy, a nie dokładamy jej kolejny raz. Nazwa w trzech liniach jest wąska – mieści się
 * też na telefonie.
 */
@Component({
  selector: 'app-brand',
  template: `
    <ion-button class="brand" aria-label="I can help U – strona główna" (click)="goHome()">
      <img src="icons/logo.svg" alt="" width="32" height="32" />
      <span class="brand-name" aria-hidden="true">
        <span class="edge">I</span>
        <span class="middle">can help</span>
        <span class="edge">U</span>
      </span>
    </ion-button>
  `,
  styles: `
    .brand {
      --padding-start: 4px;
      --padding-end: 8px;
      height: auto;
      text-transform: none;
      letter-spacing: 0;
    }
    .brand-name {
      display: flex;
      flex-direction: column;
      align-items: center;
      margin-inline-start: 6px;
      font-weight: 800;
      line-height: 1;
      color: var(--ion-color-primary);
    }
    .edge {
      font-size: 0.8125rem;
    }
    .middle {
      font-size: 0.8125rem;
      white-space: nowrap;
    }
  `,
  imports: [IonButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrandComponent {
  private readonly navCtrl = inject(NavController);

  goHome(): void {
    void this.navCtrl.navigateRoot(HOME_URL, { animated: true, animationDirection: 'back' });
  }
}
