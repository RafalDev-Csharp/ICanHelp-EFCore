import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { IonBadge, IonButton, IonIcon } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { createOutline, eyeOutline, hourglassOutline, pricetagOutline } from 'ionicons/icons';

import { PricePipe } from '../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';

import { formatOfferTerm } from './offer-labels';
import type { OfferAction, OfferView } from './offer-view';

/**
 * Oferta w wątku negocjacji: cena, termin, uzasadnienie, status i przyciski dozwolone czytającemu (`OfferView`).
 * Poprzednia wersja zmienionej oferty (`previous`) jest zwinięta. Kliknięcie przycisku zgłasza `action` – ekran woła
 * `OffersService` i przeładowuje wątek.
 */
@Component({
  selector: 'app-offer-card',
  templateUrl: './offer-card.component.html',
  styleUrls: ['./offer-card.component.scss'],
  imports: [IonIcon, IonBadge, IonButton, PricePipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OfferCardComponent {
  readonly view = input.required<OfferView>();
  /** Kiedy złożono ofertę (czas wpisu w wątku). */
  readonly at = input.required<string>();
  /** Trwa inna akcja – przyciski nieaktywne. */
  readonly busy = input(false);
  /** Chwilowe wyróżnienie po „zobacz aktualną”. */
  readonly highlighted = input(false);

  readonly action = output<OfferAction>();

  protected readonly formatOfferTerm = formatOfferTerm;

  constructor() {
    addIcons({ createOutline, eyeOutline, hourglassOutline, pricetagOutline });
  }
}
