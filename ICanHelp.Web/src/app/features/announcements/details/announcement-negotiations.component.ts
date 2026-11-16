import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { IonBadge, IonIcon, IonItem, IonLabel, IonList, IonListHeader, IonNote } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  chatbubblesOutline,
  createOutline,
  exitOutline,
  pricetagOutline,
  stopCircleOutline,
  warningOutline,
} from 'ionicons/icons';

import { PricePipe } from '../../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../../shared/pipes/time-ago.pipe';
import { formatOfferTerm, offerStateColor, offerStateLabel } from '../../offers/offer-labels';

import type { HistoryEntry, Negotiation } from './announcement-negotiations';

const HISTORY_ICONS: Readonly<Record<HistoryEntry['kind'], string>> = {
  offer: 'pricetag-outline',
  change: 'create-outline',
  abandoned: 'exit-outline',
  interrupted: 'stop-circle-outline',
};

/**
 * Negocjacje w szczegółach ogłoszenia: lista „Oferty i rozmowy” (właściciel – każdy pomocnik z ostatnią ofertą
 * i licznikiem rezygnacji; pomocnik – „Twoja negocjacja”) → wątek, oraz „Historia ogłoszenia” właściciela (oferty,
 * kontroferty, edycje, wyjścia ze zlecenia). Akcje na ofertach są w wątku – tu tylko podgląd.
 */
@Component({
  selector: 'app-announcement-negotiations',
  template: `
    @if (negotiations().length > 0) {
      <ion-list inset="true" class="negotiations">
        <ion-list-header>
          <ion-label>{{ ownerView() ? 'Oferty i rozmowy' : 'Twoja negocjacja' }}</ion-label>
        </ion-list-header>
        @for (negotiation of negotiations(); track negotiation.helperId; let last = $last) {
          <ion-item
            [button]="!!negotiation.conversationId"
            [detail]="!!negotiation.conversationId"
            (click)="openThread(negotiation)"
            [lines]="last ? 'none' : 'inset'"
          >
            <ion-icon
              [name]="negotiation.latest ? 'pricetag-outline' : 'chatbubbles-outline'"
              slot="start"
              aria-hidden="true"
            ></ion-icon>
            <ion-label>
              @if (ownerView()) {
                <h3>{{ negotiation.userName || 'Pomocnik' }}</h3>
              }
              @if (negotiation.latest; as offer) {
                <p class="negotiation-offer">
                  <strong>{{ offer.price.amount | price }}</strong>
                  <ion-badge [color]="offerStateColor(offer)">{{ offerStateLabel(offer) }}</ion-badge>
                </p>
                <p>{{ formatOfferTerm(offer.proposedStart, offer.proposedEnd) }}</p>
              } @else {
                <p>Na razie tylko wiadomości</p>
              }
              @if (ownerView() && helperReliability().get(negotiation.helperId); as reliability) {
                <p class="reliability">
                  <ion-icon name="warning-outline" color="warning" aria-hidden="true"></ion-icon> {{ reliability }}
                </p>
              }
            </ion-label>
            @if (negotiation.unreadCount > 0) {
              <ion-badge slot="end" color="primary" [attr.aria-label]="negotiation.unreadCount + ' nieprzeczytanych'">{{
                negotiation.unreadCount
              }}</ion-badge>
            }
          </ion-item>
        }
      </ion-list>
    }
    @if (history().length > 0) {
      <ion-list inset="true" class="history">
        <ion-list-header>
          <ion-label>Historia ogłoszenia</ion-label>
        </ion-list-header>
        @for (entry of history(); track entry.key; let last = $last) {
          <ion-item [lines]="last ? 'none' : 'inset'">
            <ion-icon [name]="historyIcons[entry.kind]" slot="start" aria-hidden="true"></ion-icon>
            <ion-label class="ion-text-wrap">{{ entry.text }}</ion-label>
            <ion-note slot="end">{{ entry.at | timeAgo }}</ion-note>
          </ion-item>
        }
      </ion-list>
    }
  `,
  styles: `
    :host {
      display: contents;
    }
    .negotiation-offer {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      align-items: center;
      color: var(--ion-text-color);
    }
  `,
  imports: [IonList, IonListHeader, IonItem, IonIcon, IonLabel, IonBadge, IonNote, PricePipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnnouncementNegotiationsComponent {
  readonly negotiations = input.required<Negotiation[]>();
  /** Tylko u właściciela (inaczej pusta). */
  readonly history = input.required<HistoryEntry[]>();
  /** Czytający jest właścicielem – widzi nazwy pomocników i ich liczniki. */
  readonly ownerView = input(false);
  /** Liczniki rezygnacji / przerwań pomocników (helperId → tekst). */
  readonly helperReliability = input<ReadonlyMap<string, string>>(new Map());

  /** Kliknięto negocjację, która ma wątek – id wątku. */
  readonly conversationOpened = output<string>();

  protected readonly historyIcons = HISTORY_ICONS;
  protected readonly offerStateLabel = offerStateLabel;
  protected readonly offerStateColor = offerStateColor;
  protected readonly formatOfferTerm = formatOfferTerm;

  constructor() {
    addIcons({ chatbubblesOutline, createOutline, exitOutline, pricetagOutline, stopCircleOutline, warningOutline });
  }

  protected openThread(negotiation: Negotiation): void {
    if (negotiation.conversationId) {
      this.conversationOpened.emit(negotiation.conversationId);
    }
  }
}
