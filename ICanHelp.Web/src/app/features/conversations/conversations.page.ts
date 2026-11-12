import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import {
  IonBadge,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonInfiniteScroll,
  IonInfiniteScrollContent,
  IonItem,
  IonLabel,
  IonList,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSegment,
  IonSegmentButton,
  IonSpinner,
  IonThumbnail,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { chatbubblesOutline, cloudOfflineOutline, imageOutline, warningOutline } from 'ionicons/icons';
import type { Observable } from 'rxjs';
import { filter } from 'rxjs';

import type { MyConversationDto, OfferParty } from '../../core/api/api-client';
import { ConversationsClient } from '../../core/api/api-client';
import { apiResourceUrl } from '../../core/api/api.providers';
import { concernsConversation } from '../../core/notifications/notification-texts';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PricePipe } from '../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { statusColor, statusLabel } from '../announcements/announcement-labels';
import { offerStatusLabel } from '../offers/offer-labels';

/** Rozmów na stronę (`GET api/me/conversations`). */
export const CONVERSATIONS_PAGE_SIZE = 20;

type RoleFilter = 'all' | OfferParty;

interface CompletableTarget {
  complete(): Promise<void>;
}

/**
 * Zakładka „Rozmowy” (tylko zalogowani): wątki negocjacji od ostatniej aktywności – jako zleceniodawca (moje
 * ogłoszenia) i jako pomocnik, z ostatnią ofertą, podglądem wiadomości i liczbą nieprzeczytanych.
 * Odświeżana przy każdym wejściu (po powrocie z wątku licznik ma zniknąć).
 */
@Component({
  selector: 'app-conversations',
  templateUrl: './conversations.page.html',
  styleUrls: ['./conversations.page.scss'],
  imports: [
    BrandComponent,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonContent,
    IonSegment,
    IonSegmentButton,
    IonRefresher,
    IonRefresherContent,
    IonList,
    IonItem,
    IonThumbnail,
    IonLabel,
    IonNote,
    IonBadge,
    IonIcon,
    IonSpinner,
    IonButton,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    EmptyStateComponent,
    PricePipe,
    TimeAgoPipe,
  ],
})
export class ConversationsPage implements ViewWillEnter {
  private readonly client = inject(ConversationsClient);

  readonly role = signal<RoleFilter>('all');
  readonly items = signal<MyConversationDto[]>([]);
  readonly state = signal<'loading' | 'ready' | 'failed'>('loading');
  readonly complete = signal(false);
  private page = 1;

  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;
  protected readonly offerStatusLabel = offerStatusLabel;
  protected readonly thumbnail = apiResourceUrl;

  constructor() {
    addIcons({ chatbubblesOutline, cloudOfflineOutline, imageOutline, warningOutline });

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
