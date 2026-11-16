import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IonBadge, IonButton, IonIcon } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { notificationsOutline } from 'ionicons/icons';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationsService } from '../../../core/notifications/notifications.service';

/** Tekst plakietki: powyżej 99 – „99+”, 0 – bez plakietki. */
export function unreadBadgeText(count: number): string {
  return count <= 0 ? '' : count > 99 ? '99+' : String(count);
}

/**
 * Dzwonek z plakietką nieprzeczytanych w nagłówku stron zakładek (`ion-buttons slot="end"`) → `/notifications`
 * (zamiast osobnej zakładki). Niezalogowany nie ma powiadomień – dzwonka nie widać.
 */
@Component({
  selector: 'app-notifications-bell',
  template: `
    @if (auth.isAuthenticated()) {
      <ion-button routerLink="/notifications" [attr.aria-label]="label()">
        <ion-icon slot="icon-only" name="notifications-outline" aria-hidden="true"></ion-icon>
        @if (badge(); as badge) {
          <ion-badge color="danger" aria-hidden="true">{{ badge }}</ion-badge>
        }
      </ion-button>
    }
  `,
  styles: `
    ion-button {
      position: relative;
      overflow: visible;
    }
    ion-badge {
      position: absolute;
      top: 2px;
      right: 0;
      min-width: 18px;
      padding: 2px 4px;
      font-size: 0.6875rem;
    }
  `,
  imports: [RouterLink, IonButton, IonIcon, IonBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsBellComponent {
  private readonly notifications = inject(NotificationsService);
  protected readonly auth = inject(AuthService);

  readonly badge = computed(() => unreadBadgeText(this.notifications.unreadCount()));
  readonly label = computed(() => {
    const badge = this.badge();
    return badge ? `Powiadomienia, ${badge} nieprzeczytanych` : 'Powiadomienia';
  });

  constructor() {
    addIcons({ notificationsOutline });
  }
}
