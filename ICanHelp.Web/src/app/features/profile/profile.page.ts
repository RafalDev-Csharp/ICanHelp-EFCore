import { Component } from '@angular/core';
import { IonContent, IonHeader, IonTitle, IonToolbar } from '@ionic/angular';

import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.page.html',
  imports: [IonHeader, IonToolbar, IonTitle, IonContent, EmptyStateComponent],
})
export class ProfilePage {}
