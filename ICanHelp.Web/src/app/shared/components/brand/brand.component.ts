import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { IonButton, NavController } from '@ionic/angular';

/** Strona główna aplikacji – tam prowadzi logo. */
export const HOME_URL = '/announcements';

/**
 * Logo i nazwa „I can help U” w nagłówku (wewnątrz `ion-buttons`) → strona główna (lista ogłoszeń). `navigateRoot`
 * czyści stos zakładki Ogłoszenia, więc ze szczegółów wracamy do listy, a nie dokładamy jej kolejny raz. Na wąskim
 * ekranie zostaje sama ikona (nazwę ma `aria-label`).
 */
@Component({
  selector: 'app-brand',
  template: `
    <ion-button class="brand" aria-label="I can help U – strona główna" (click)="goHome()">
      <img src="icons/icon-72x72.png" alt="" width="28" height="28" />
      <span class="brand-name">I can help U</span>
    </ion-button>
  `,
  styles: `
    .brand {
      --padding-start: 4px;
      --padding-end: 8px;
      text-transform: none;
    }
    img {
      border-radius: 6px;
    }
    .brand-name {
      margin-inline-start: 8px;
      font-weight: 700;
      white-space: nowrap;
    }
    @media (max-width: 575px) {
      .brand-name {
        display: none;
      }
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
