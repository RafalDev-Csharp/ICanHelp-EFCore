import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonInfiniteScroll,
  IonInfiniteScrollContent,
  IonItemDivider,
  IonItemGroup,
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
import {
  briefcaseOutline,
  chatbubblesOutline,
  chevronDown,
  chevronUp,
  cloudOfflineOutline,
  handLeftOutline,
} from 'ionicons/icons';
import type { Observable } from 'rxjs';
import { filter, of, switchMap } from 'rxjs';

import type { AnnouncementDto, MyConversationDto, OfferParty } from '../../core/api/api-client';
import { AnnouncementsClient, ConversationsClient } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import { CategoriesService } from '../../core/categories/categories.service';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { NotificationsBellComponent } from '../../shared/components/notifications-bell/notifications-bell.component';
import { AnnouncementCardComponent } from '../announcements/list/announcement-card.component';
import { ConversationItemComponent } from '../conversations/conversation-item.component';

import { clientOrderGroup, groupOrders, helperOrderGroup } from './order-groups';

/** Elementów na stronę (moje ogłoszenia / moje wątki pomocnika). */
export const ORDERS_PAGE_SIZE = 50;

type LoadState = 'loading' | 'ready' | 'failed';

interface CompletableTarget {
  complete(): Promise<void>;
}

/** Lista doładowywana stronami – osobna dla „Zlecam” i „Pomagam”. */
class PagedList<T> {
  readonly items = signal<T[]>([]);
  readonly state = signal<LoadState>('loading');
  readonly complete = signal(false);
  private page = 1;

  constructor(
    private readonly fetch: (page: number) => Observable<T[]>,
    private readonly key: (item: T) => string,
  ) {}

  reload(done?: () => void): void {
    if (this.items().length === 0) {
      this.state.set('loading');
    }
    this.fetch(1).subscribe({
      next: (items) => {
        this.page = 1;
        this.items.set(items);
        this.complete.set(items.length < ORDERS_PAGE_SIZE);
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

  loadMore(done: () => void): void {
    this.fetch(this.page + 1).subscribe({
      next: (items) => {
        this.page += 1;
        this.items.update((current) => {
          const keys = new Set(current.map(this.key));
          return [...current, ...items.filter((item) => !keys.has(this.key(item)))];
        });
        this.complete.set(items.length < ORDERS_PAGE_SIZE);
        done();
      },
      error: () => done(),
    });
  }
}

/**
 * Zakładka „Zlecenia” (tylko zalogowani), segment **Zlecam / Pomagam**, pogrupowane po statusie (w trakcie negocjacji,
 * przyjęte, w realizacji, zakończone). Zlecam – moje ogłoszenia (wszystkie statusy → szczegóły z ofertami i rozmowami);
 * Pomagam – moje wątki jako pomocnik (pytania i oferty). Ogłoszenia po akceptacji są tylko tutaj (lista „Szukaj”
 * pokazuje wyłącznie otwarte). Odświeżana przy każdym wejściu i po powiadomieniu o wątku. F11 D: aktywne na górze,
 * zakończone / anulowane w osobnej, domyślnie zwiniętej sekcji „Zakończone (N)”, wyszarzone.
 */
@Component({
  selector: 'app-orders',
  templateUrl: './orders.page.html',
  styleUrls: ['./orders.page.scss'],
  imports: [
    BrandComponent,
    NotificationsBellComponent,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonButtons,
    IonButton,
    IonIcon,
    IonContent,
    IonSegment,
    IonSegmentButton,
    IonLabel,
    IonRefresher,
    IonRefresherContent,
    IonList,
    IonItemGroup,
    IonItemDivider,
    IonSpinner,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    EmptyStateComponent,
    AnnouncementCardComponent,
    ConversationItemComponent,
  ],
})
export class OrdersPage implements ViewWillEnter {
  private readonly announcementsClient = inject(AnnouncementsClient);
  private readonly conversationsClient = inject(ConversationsClient);
  private readonly auth = inject(AuthService);
  private readonly categories = inject(CategoriesService);

  readonly role = signal<OfferParty>('Client');

  readonly mine = new PagedList<AnnouncementDto>(
    (page) => this.fetchMyAnnouncements(page),
    (item) => item.id,
  );
  readonly helping = new PagedList<MyConversationDto>(
    (page) => this.conversationsClient.getMine('Helper', undefined, undefined, page, ORDERS_PAGE_SIZE),
    (item) => item.conversationId,
  );

  readonly current = computed(() => (this.role() === 'Client' ? this.mine : this.helping));
  readonly clientGroups = computed(() => groupOrders(this.mine.items(), clientOrderGroup));
  readonly helperGroups = computed(() => groupOrders(this.helping.items(), helperOrderGroup));
  /** Sekcja „Zakończone” (zakończone, anulowane, wybrano innego) – domyślnie zwinięta, aktywne są na górze. */
  readonly finishedOpen = signal(false);

  constructor() {
    addIcons({
      briefcaseOutline,
      chatbubblesOutline,
      chevronDown,
      chevronUp,
      cloudOfflineOutline,
      handLeftOutline,
    });

    // Nowa wiadomość / oferta / krok zlecenia (hub albo push) – lista od razu z nowym stanem.
    inject(NotificationsService)
      .activity$.pipe(
        filter((activity) => !!activity.conversationId),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.current().reload());
  }

  toggleFinished(): void {
    this.finishedOpen.update((open) => !open);
  }

  /** Przy każdym wejściu – po akcji w szczegółach albo w wątku status mógł się zmienić. */
  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    this.current().reload();
  }

  setRole(value: unknown): void {
    const role = value === 'Helper' ? 'Helper' : 'Client';
    if (role !== this.role()) {
      this.role.set(role);
      this.current().reload();
    }
  }

  refresh(event: Event): void {
    this.current().reload(() => void (event.target as unknown as CompletableTarget).complete());
  }

  loadMore(event: Event): void {
    this.current().loadMore(() => void (event.target as unknown as CompletableTarget).complete());
  }

  private fetchMyAnnouncements(page: number): Observable<AnnouncementDto[]> {
    const user = this.auth.currentUser();
    return (user ? of(user) : this.auth.loadCurrentUser()).pipe(
      switchMap((owner) =>
        this.announcementsClient.getAll(
          owner?.id,
          undefined,
          undefined,
          undefined,
          undefined,
          undefined,
          undefined,
          undefined,
          page,
          ORDERS_PAGE_SIZE,
        ),
      ),
    );
  }
}
