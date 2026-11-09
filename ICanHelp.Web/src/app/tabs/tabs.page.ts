import { Component, computed, inject } from '@angular/core';
import { IonBadge, IonIcon, IonLabel, IonTabBar, IonTabButton, IonTabs } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  addCircleOutline,
  chatbubblesOutline,
  megaphoneOutline,
  notificationsOutline,
  personCircleOutline,
} from 'ionicons/icons';

import { NotificationsService } from '../core/notifications/notifications.service';

@Component({
  selector: 'app-tabs',
  templateUrl: 'tabs.page.html',
  styleUrls: ['tabs.page.scss'],
  imports: [IonTabs, IonTabBar, IonTabButton, IonIcon, IonLabel, IonBadge],
})
export class TabsPage {
  private readonly notifications = inject(NotificationsService);

  /** Plakietka zakładki „Powiadomienia” (powyżej 99 – „99+”); 0 – bez plakietki. */
  readonly unreadBadge = computed(() => {
    const count = this.notifications.unreadCount();
    return count <= 0 ? '' : count > 99 ? '99+' : String(count);
  });

  constructor() {
    // Ikony rejestrujemy raz tutaj – strony zakładek używają tych samych nazw.
    addIcons({ megaphoneOutline, addCircleOutline, chatbubblesOutline, notificationsOutline, personCircleOutline });
    // Zakładki = aplikacja działa: od teraz powiadomienia na żywo (gdy użytkownik jest zalogowany).
    this.notifications.start();
  }
}
