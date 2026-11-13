import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IonBadge, IonIcon, IonItem, IonLabel, IonNote, IonThumbnail } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { imageOutline, warningOutline } from 'ionicons/icons';

import type { MyConversationDto } from '../../core/api/api-client';
import { apiResourceUrl } from '../../core/api/api.providers';
import { PricePipe } from '../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { statusColor, statusLabel } from '../announcements/announcement-labels';
import { offerStateLabel } from '../offers/offer-labels';

/**
 * Wątek na liście (`ion-list`): miniatura, tytuł ogłoszenia, druga strona, status, ostatnia oferta, podgląd wiadomości
 * i nieprzeczytane → `/conversations/:id`. Wspólny dla „Wszystkie rozmowy” i „Zlecenia → Pomagam”.
 */
@Component({
  selector: 'app-conversation-item',
  template: `
    @let item = conversation();
    <ion-item [routerLink]="['/conversations', item.conversationId]" [detail]="false" class="conversation">
      <ion-thumbnail slot="start">
        @if (thumbnail(item.thumbnailUrl); as url) {
          <img [src]="url" alt="" loading="lazy" />
        } @else {
          <div class="no-photo"><ion-icon name="image-outline" aria-hidden="true"></ion-icon></div>
        }
      </ion-thumbnail>
      <ion-label>
        <h2 [class.unread]="item.unreadCount > 0">{{ item.title }}</h2>
        <p>
          {{ item.myRole === 'Client' ? 'Pomocnik' : 'Zleceniodawca' }}: {{ item.otherParty.userName }}
          <ion-badge [color]="statusColor(item.announcementStatus)">{{
            statusLabel(item.announcementStatus)
          }}</ion-badge>
        </p>
        @if (item.lastOffer; as offer) {
          <p class="last-offer">{{ offer.price.amount | price }} · {{ offerStateLabel(offer) }}</p>
        } @else {
          <p class="last-offer">Pytania i odpowiedzi – bez oferty</p>
        }
        @if (item.lastMessagePreview) {
          <p class="preview">{{ item.lastMessagePreview }}</p>
        }
        @if (item.announcementChangedSinceLastRead) {
          <p class="changed">
            <ion-icon name="warning-outline" color="warning" aria-hidden="true"></ion-icon>
            Ogłoszenie zmienione od Twojej ostatniej wizyty
          </p>
        }
      </ion-label>
      <div slot="end" class="side">
        <ion-note>{{ item.lastActivityAt | timeAgo }}</ion-note>
        @if (item.unreadCount > 0) {
          <ion-badge color="primary" [attr.aria-label]="item.unreadCount + ' nieprzeczytanych'">{{
            item.unreadCount
          }}</ion-badge>
        }
      </div>
    </ion-item>
  `,
  styleUrls: ['./conversation-item.component.scss'],
  imports: [RouterLink, IonItem, IonThumbnail, IonIcon, IonLabel, IonBadge, IonNote, PricePipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConversationItemComponent {
  readonly conversation = input.required<MyConversationDto>();

  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;
  protected readonly offerStateLabel = offerStateLabel;
  protected readonly thumbnail = apiResourceUrl;

  constructor() {
    addIcons({ imageOutline, warningOutline });
  }
}
