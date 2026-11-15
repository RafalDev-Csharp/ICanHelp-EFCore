import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInfiniteScroll,
  IonInfiniteScrollContent,
  IonLabel,
  IonList,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { chatbubblesOutline } from 'ionicons/icons';
import { filter } from 'rxjs';

import type { MyConversationDto, OfferParty } from '../../core/api/api-client';
import { ConversationsClient } from '../../core/api/api-client';
import { concernsConversation } from '../../core/notifications/notification-texts';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { NotificationsBellComponent } from '../../shared/components/notifications-bell/notifications-bell.component';
import { PagedList } from '../../shared/data/remote-list';

import { ConversationItemComponent } from './conversation-item.component';

/** Rozmów na stronę (`GET api/me/conversations`). */
export const CONVERSATIONS_PAGE_SIZE = 20;

type RoleFilter = 'all' | OfferParty;

/**
 * „Wszystkie rozmowy” (`/conversations/all`, z nagłówka zakładki „Zlecenia”; tylko zalogowani): wątki od ostatniej
 * aktywności – jako zleceniodawca i jako pomocnik, z ostatnią ofertą, podglądem wiadomości i liczbą nieprzeczytanych.
 * Odświeżana przy każdym wejściu (po powrocie z wątku licznik ma zniknąć).
 */
@Component({
  selector: 'app-conversations',
  templateUrl: './conversations.page.html',
  styleUrls: ['./conversations.page.scss'],
  imports: [
    BrandComponent,
    NotificationsBellComponent,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonBackButton,
    IonContent,
    IonSegment,
    IonSegmentButton,
    IonRefresher,
    IonRefresherContent,
    IonList,
    IonLabel,
    IonButton,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
    ConversationItemComponent,
  ],
})
export class ConversationsPage implements ViewWillEnter {
  private readonly client = inject(ConversationsClient);

  readonly role = signal<RoleFilter>('all');
  protected readonly list = new PagedList<MyConversationDto>(
    (page) => {
      const role = this.role();
      return this.client.getMine(
        role === 'all' ? undefined : role,
        undefined,
        undefined,
        page,
        CONVERSATIONS_PAGE_SIZE,
      );
    },
    (item) => item.conversationId,
    CONVERSATIONS_PAGE_SIZE,
  );

  constructor() {
    addIcons({ chatbubblesOutline });

    // Nowa wiadomość / oferta (SignalR) – lista od razu z nowym podglądem i licznikiem.
    inject(NotificationsService)
      .received$.pipe(filter(concernsConversation), takeUntilDestroyed())
      .subscribe(() => this.list.reload());
  }

  ionViewWillEnter(): void {
    this.list.reload();
  }

  setRole(value: unknown): void {
    const role = value === 'Client' || value === 'Helper' ? value : 'all';
    if (role !== this.role()) {
      this.role.set(role);
      this.list.items.set([]);
      this.list.reload();
    }
  }
}
