import { Component, computed, inject } from '@angular/core';
import {
  IonButton,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonSegment,
  IonSegmentButton,
  IonSpinner,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { contrastOutline, downloadOutline, notificationsOutline, shareOutline } from 'ionicons/icons';

import { AuthService } from '../../core/auth/auth.service';
import { InstallPromptService } from '../../core/pwa/install-prompt.service';
import { PushService } from '../../core/pwa/push.service';
import type { ThemeMode } from '../../core/theme/theme.service';
import { ThemeService, parseThemeMode } from '../../core/theme/theme.service';

/**
 * Sekcja „Aplikacja” w profilu (F7): motyw (systemowy / jasny / ciemny – zawsze, także bez logowania), instalacja PWA
 * (Android – przycisk, iPhone – instrukcja) i powiadomienia push (stan + „Włącz”). Bez konfiguracji Firebase /
 * w `ng serve` część push się nie pokazuje.
 */
@Component({
  selector: 'app-app-settings',
  templateUrl: './app-settings.component.html',
  styles: `
    .theme-body {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      padding: 10px 0;
    }
    .theme-title {
      margin: 0;
      font-size: 1rem;
      font-weight: 400;
    }
    .theme-switch {
      width: 100%;
      max-width: 360px;
      margin-top: 8px;
    }
    .theme-switch ion-segment-button {
      min-width: 0;
      min-height: 36px;
      --padding-start: 4px;
      --padding-end: 4px;
      font-size: 0.875rem;
      text-transform: none;
      letter-spacing: 0;
    }
  `,
  imports: [
    IonList,
    IonListHeader,
    IonItem,
    IonIcon,
    IonLabel,
    IonNote,
    IonButton,
    IonSpinner,
    IonSegment,
    IonSegmentButton,
  ],
})
export class AppSettingsComponent {
  protected readonly auth = inject(AuthService);
  protected readonly install = inject(InstallPromptService);
  protected readonly push = inject(PushService);
  protected readonly theme = inject(ThemeService);

  protected readonly themeOptions: readonly { mode: ThemeMode; label: string }[] = [
    { mode: 'system', label: 'Systemowy' },
    { mode: 'light', label: 'Jasny' },
    { mode: 'dark', label: 'Ciemny' },
  ];

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

  constructor() {
    addIcons({ contrastOutline, downloadOutline, notificationsOutline, shareOutline });
  }

  setTheme(value: unknown): void {
    this.theme.setMode(parseThemeMode(typeof value === 'string' ? value : null));
  }

  enablePush(): void {
    void this.push.enable();
  }

  installApp(): void {
    void this.install.install();
  }
}
