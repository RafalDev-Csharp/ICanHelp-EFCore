import { Component, computed, inject } from '@angular/core';
import { IonButton, IonIcon, IonItem, IonLabel, IonList, IonListHeader, IonNote, IonSpinner } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { downloadOutline, notificationsOutline, shareOutline } from 'ionicons/icons';

import { AuthService } from '../../core/auth/auth.service';
import { InstallPromptService } from '../../core/pwa/install-prompt.service';
import { PushService } from '../../core/pwa/push.service';

/**
 * Sekcja „Aplikacja” w profilu (F7): instalacja PWA (Android – przycisk, iPhone – instrukcja) i powiadomienia push
 * (stan + „Włącz”). Bez konfiguracji Firebase / w `ng serve` część push się nie pokazuje.
 */
@Component({
  selector: 'app-app-settings',
  templateUrl: './app-settings.component.html',
  imports: [IonList, IonListHeader, IonItem, IonIcon, IonLabel, IonNote, IonButton, IonSpinner],
})
export class AppSettingsComponent {
  protected readonly auth = inject(AuthService);
  protected readonly install = inject(InstallPromptService);
  protected readonly push = inject(PushService);

  /** Tekst stanu push (`null` – sekcji nie pokazujemy). */
  protected readonly pushNote = computed<string | null>(() => {
    switch (this.push.state()) {
      case 'enabled':
        return 'Włączone na tym urządzeniu';
      case 'denied':
        return 'Zablokowane – zmień w ustawieniach przeglądarki';
      case 'needs-install':
        return 'Najpierw dodaj aplikację do ekranu początkowego';
      case 'unsupported':
        return 'Ta przeglądarka ich nie obsługuje';
      case 'default':
        return 'Wyłączone';
      default:
        return null;
    }
  });

  protected readonly visible = computed(
    () => this.install.canPrompt() || this.install.showIosHint() || (this.auth.isAuthenticated() && !!this.pushNote()),
  );

  constructor() {
    addIcons({ downloadOutline, notificationsOutline, shareOutline });
  }

  enablePush(): void {
    void this.push.enable();
  }

  installApp(): void {
    void this.install.install();
  }
}
