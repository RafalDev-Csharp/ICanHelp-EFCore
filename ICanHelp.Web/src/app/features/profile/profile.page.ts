import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { documentTextOutline, heartOutline, notificationsOutline } from 'ionicons/icons';

import { AuthService } from '../../core/auth/auth.service';
import { PushService } from '../../core/pwa/push.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { parseApiDate } from '../../shared/pipes/time-ago.pipe';

import { AppSettingsComponent } from './app-settings.component';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.page.html',
  imports: [
    BrandComponent,
    DatePipe,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonContent,
    IonList,
    IonListHeader,
    IonItem,
    IonIcon,
    IonLabel,
    IonNote,
    IonButton,
    IonSpinner,
    EmptyStateComponent,
    AppSettingsComponent,
  ],
})
export class ProfilePage implements ViewWillEnter {
  private readonly router = inject(Router);
  private readonly push = inject(PushService);
  protected readonly auth = inject(AuthService);
  protected readonly loading = signal(false);
  protected readonly parseApiDate = parseApiDate;

  constructor() {
    addIcons({ documentTextOutline, heartOutline, notificationsOutline });
  }

  /** Zakładki Ionic zostają w pamięci – dane odświeżamy przy każdym wejściu na zakładkę. */
  ionViewWillEnter(): void {
    if (!this.auth.isAuthenticated()) {
      return;
    }
    this.loading.set(true);
    this.auth.loadCurrentUser().subscribe({
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }

  /** Najpierw urządzenie push (`DELETE api/me/devices/{id}` wymaga jeszcze sesji), potem wylogowanie. */
  async signOut(): Promise<void> {
    await this.push.unregister();
    this.auth.signOut().subscribe(() => void this.router.navigateByUrl('/announcements', { replaceUrl: true }));
  }
}
