import { Component } from '@angular/core';
import { IonIcon, IonLabel, IonTabBar, IonTabButton, IonTabs } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  addCircleOutline,
  chatbubblesOutline,
  megaphoneOutline,
  notificationsOutline,
  personCircleOutline,
} from 'ionicons/icons';

@Component({
  selector: 'app-tabs',
  templateUrl: 'tabs.page.html',
  styleUrls: ['tabs.page.scss'],
  imports: [IonTabs, IonTabBar, IonTabButton, IonIcon, IonLabel],
})
export class TabsPage {
  constructor() {
    // Ikony rejestrujemy raz tutaj – strony zakładek używają tych samych nazw.
    addIcons({ megaphoneOutline, addCircleOutline, chatbubblesOutline, notificationsOutline, personCircleOutline });
  }
}
