import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IonBadge, IonButton, IonIcon } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { imageOutline, warningOutline } from 'ionicons/icons';

import type { ConversationDto } from '../../core/api/api-client';
import { statusColor, statusLabel } from '../announcements/announcement-labels';
import { oppositeParty, partyLabel } from '../offers/offer-labels';

import { otherPartyId } from './conversation-thread';

/**
 * Karta ogłoszenia u góry wątku: miniatura, tytuł i status ogłoszenia, rola czytającego, licznik rezygnacji / przerwań
 * drugiej strony, link do jej profilu i „Zobacz ogłoszenie”.
 */
@Component({
  selector: 'app-conversation-subject',
  template: `
    @let item = thread();
    <div class="subject page-column">
      <a class="subject-thumb" [routerLink]="['/announcements', item.announcementId]" tabindex="-1" aria-hidden="true">
        @if (thumbnailUrl(); as url) {
          <img [src]="url" alt="" />
        } @else {
          <ion-icon name="image-outline"></ion-icon>
        }
      </a>
      <div class="subject-text">
        <h2>{{ item.announcementTitle }}</h2>
        <p>
          <ion-badge [color]="statusColor(item.announcementStatus)">{{
            statusLabel(item.announcementStatus)
          }}</ion-badge>
          · jesteś: {{ partyLabel(item.myRole).toLowerCase() }}
        </p>
        @if (otherReliability(); as reliability) {
          <p class="reliability">
            <ion-icon name="warning-outline" color="warning" aria-hidden="true"></ion-icon>
            {{ otherParty() }}: {{ reliability }}
          </p>
        }
        <a class="profile-link" [routerLink]="['/users', otherId()]">Profil: {{ otherParty().toLowerCase() }}</a>
      </div>
      <ion-button
        class="subject-link"
        size="small"
        fill="outline"
        [routerLink]="['/announcements', item.announcementId]"
        >Zobacz ogłoszenie</ion-button
      >
    </div>
  `,
  styleUrls: ['./conversation-subject.component.scss'],
  imports: [RouterLink, IonIcon, IonBadge, IonButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConversationSubjectComponent {
  readonly thread = input.required<ConversationDto>();
  /** Pierwsze zdjęcie ogłoszenia; brak zdjęć albo błąd – ikona. */
  readonly thumbnailUrl = input<string | undefined>(undefined);
  /** „1 rezygnacja · 2 przerwane zlecenia” drugiej strony (pusty = brak). */
  readonly otherReliability = input('');

  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;
  protected readonly partyLabel = partyLabel;

  /** „Pomocnik” / „Zleceniodawca” – druga strona wątku. */
  protected readonly otherParty = computed(() => partyLabel(oppositeParty(this.thread().myRole)));
  protected readonly otherId = computed(() => otherPartyId(this.thread()));

  constructor() {
    addIcons({ imageOutline, warningOutline });
  }
}
