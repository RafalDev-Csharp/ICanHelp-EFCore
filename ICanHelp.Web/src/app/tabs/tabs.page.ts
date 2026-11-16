import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { IonIcon, IonLabel, IonTabBar, IonTabButton, IonTabs, NavController } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { addCircleOutline, briefcaseOutline, heartOutline, personCircleOutline, searchOutline } from 'ionicons/icons';

import { NotificationsService } from '../core/notifications/notifications.service';
import { PushService } from '../core/pwa/push.service';

/**
 * Dolny pasek: Szukaj · Ulubione · Dodaj · Zlecenia · Konto (F11 B). Powiadomienia nie mają zakładki – dzwonek
 * z plakietką jest w nagłówku stron zakładek (`app-notifications-bell`).
 */
@Component({
  selector: 'app-tabs',
  templateUrl: 'tabs.page.html',
  styleUrls: ['tabs.page.scss'],
  imports: [IonTabs, IonTabBar, IonTabButton, IonIcon, IonLabel],
})
export class TabsPage {
  private readonly router = inject(Router);
  private readonly navCtrl = inject(NavController);

  constructor() {
    // Ikony rejestrujemy raz tutaj – strony zakładek używają tych samych nazw.
    addIcons({ searchOutline, heartOutline, addCircleOutline, briefcaseOutline, personCircleOutline });
    // Zakładki = aplikacja działa: od teraz powiadomienia na żywo (gdy użytkownik jest zalogowany).
    inject(NotificationsService).start();
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
