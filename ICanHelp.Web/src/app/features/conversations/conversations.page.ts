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
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { chatbubblesOutline, cloudOfflineOutline } from 'ionicons/icons';
import type { Observable } from 'rxjs';
import { filter } from 'rxjs';

import type { MyConversationDto, OfferParty } from '../../core/api/api-client';
import { ConversationsClient } from '../../core/api/api-client';
import { concernsConversation } from '../../core/notifications/notification-texts';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { NotificationsBellComponent } from '../../shared/components/notifications-bell/notifications-bell.component';

import { ConversationItemComponent } from './conversation-item.component';

/** Rozmów na stronę (`GET api/me/conversations`). */
export const CONVERSATIONS_PAGE_SIZE = 20;

type RoleFilter = 'all' | OfferParty;

interface CompletableTarget {
  complete(): Promise<void>;
}

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
    IonSpinner,
    IonButton,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    EmptyStateComponent,
    ConversationItemComponent,
  ],
})
export class ConversationsPage implements ViewWillEnter {
  private readonly client = inject(ConversationsClient);

  readonly role = signal<RoleFilter>('all');
  readonly items = signal<MyConversationDto[]>([]);
  readonly state = signal<'loading' | 'ready' | 'failed'>('loading');
  readonly complete = signal(false);
  private page = 1;

  constructor() {
    addIcons({ chatbubblesOutline, cloudOfflineOutline });

    // Nowa wiadomość / oferta (SignalR) – lista od razu z nowym podglądem i licznikiem.
    inject(NotificationsService)
      .received$.pipe(filter(concernsConversation), takeUntilDestroyed())
      .subscribe(() => this.reload());
  }

  ionViewWillEnter(): void {
    this.reload();
  }

  setRole(value: unknown): void {
    const role = value === 'Client' || value === 'Helper' ? value : 'all';
    if (role !== this.role()) {
      this.role.set(role);
      this.items.set([]);
      this.reload();
    }
  }

  reload(done?: () => void): void {
    if (this.items().length === 0) {
      this.state.set('loading');
    }
    this.fetch(1).subscribe({
      next: (items) => {
        this.page = 1;
        this.items.set(items);
        this.complete.set(items.length < CONVERSATIONS_PAGE_SIZE);
        this.state.set('ready');
        done?.();
      },
      error: () => {
        if (this.items().length === 0) {
          this.state.set('failed');
        }
        done?.();
      },
    });
  }

  refresh(event: Event): void {
    this.reload(() => void (event.target as unknown as CompletableTarget).complete());
  }

  loadMore(event: Event): void {
    const target = event.target as unknown as CompletableTarget;
    this.fetch(this.page + 1).subscribe({
      next: (items) => {
        this.page += 1;
        this.items.update((current) => [
          ...current,
          ...items.filter((item) => !current.some((c) => c.conversationId === item.conversationId)),
        ]);
        this.complete.set(items.length < CONVERSATIONS_PAGE_SIZE);
        void target.complete();
      },
      error: () => void target.complete(),
    });
  }

  private fetch(page: number): Observable<MyConversationDto[]> {
    const role = this.role();
    return this.client.getMine(role === 'all' ? undefined : role, undefined, undefined, page, CONVERSATIONS_PAGE_SIZE);
  }
}
