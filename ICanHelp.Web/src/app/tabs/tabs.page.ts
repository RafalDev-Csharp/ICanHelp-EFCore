import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { IonBadge, IonIcon, IonLabel, IonTabBar, IonTabButton, IonTabs, NavController } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  addCircleOutline,
  chatbubblesOutline,
  megaphoneOutline,
  notificationsOutline,
  personCircleOutline,
} from 'ionicons/icons';

import { NotificationsService } from '../core/notifications/notifications.service';
import { PushService } from '../core/pwa/push.service';

@Component({
  selector: 'app-tabs',
  templateUrl: 'tabs.page.html',
  styleUrls: ['tabs.page.scss'],
  imports: [IonTabs, IonTabBar, IonTabButton, IonIcon, IonLabel, IonBadge],
})
export class TabsPage {
  private readonly notifications = inject(NotificationsService);
  private readonly router = inject(Router);
  private readonly navCtrl = inject(NavController);

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
    // Push (F7): ponowny zapis urządzenia, gdy zgoda już jest; o zgodę pytamy dopiero po akcji użytkownika.
    inject(PushService).start();
  }

  /**
   * Stuknięcie zakładki zawsze prowadzi do jej korzenia (`/announcements`, `/profile`…), także przy zmianie zakładki –
   * domyślnie Ionic wraca do ostatniego ekranu zakładki (np. szczegółów zamiast listy). `navigateRoot` czyści stos tej
   * zakładki, a widok korzenia (z filtrami listy, szkicem formularza) jest używany ponownie. Zdarzenie zatrzymujemy, żeby
   * nie obsłużył go jeszcze `ion-tabs`.
   */
  openTabRoot(event: Event): void {
    const tab = (event as CustomEvent<{ tab?: string } | undefined>).detail?.tab;
    if (!tab) {
      return;
    }
    event.stopPropagation();
    const root = `/${tab}`;
    if (this.router.url.split(/[?#]/)[0] === root) {
      return;
    }
    void this.navCtrl.navigateRoot(root, { animated: true, animationDirection: 'back' });
  }
}
