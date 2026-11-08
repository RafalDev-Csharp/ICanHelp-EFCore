import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  IonButton,
  IonContent,
  IonHeader,
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

import { AuthService } from '../../core/auth/auth.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { parseApiDate } from '../../shared/pipes/time-ago.pipe';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.page.html',
  imports: [
    DatePipe,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonContent,
    IonList,
    IonListHeader,
    IonItem,
    IonLabel,
    IonNote,
    IonButton,
    IonSpinner,
    EmptyStateComponent,
  ],
})
export class ProfilePage implements ViewWillEnter {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly loading = signal(false);
  protected readonly parseApiDate = parseApiDate;

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

  signOut(): void {
    this.auth.signOut().subscribe(() => void this.router.navigateByUrl('/announcements', { replaceUrl: true }));
  }
}
