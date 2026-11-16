import { Component, computed, inject, signal } from '@angular/core';
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
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { briefcaseOutline, chatbubblesOutline, chevronDown, chevronUp, handLeftOutline } from 'ionicons/icons';
import type { Observable } from 'rxjs';
import { of, switchMap } from 'rxjs';

import type { AnnouncementDto, MyConversationDto, OfferParty } from '../../core/api/api-client';
import { AnnouncementsClient, ConversationsClient } from '../../core/api/api-client';
import { AuthService } from '../../core/auth/auth.service';
import { CategoriesService } from '../../core/categories/categories.service';
import { reloadOnActivity } from '../../core/notifications/reload-on-activity';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { NotificationsBellComponent } from '../../shared/components/notifications-bell/notifications-bell.component';
import { PagedList } from '../../shared/data/remote-list';
import { AnnouncementCardComponent } from '../announcements/list/announcement-card.component';
import { ConversationItemComponent } from '../conversations/conversation-item.component';

import type { OrderGroupKey } from './order-groups';
import { clientOrderGroup, groupOrders, helperOrderGroup } from './order-groups';

/** Elementów na stronę (moje ogłoszenia / moje wątki pomocnika). */
export const ORDERS_PAGE_SIZE = 50;

/** Grupa aktywnej listy do narysowania: w „Zlecam” – ogłoszenia, w „Pomagam” – wątki (druga lista pusta). */
interface OrderGroupView {
  key: OrderGroupKey;
  label: string;
  count: number;
  announcements: AnnouncementDto[];
  conversations: MyConversationDto[];
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
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
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
    ORDERS_PAGE_SIZE,
  );
  readonly helping = new PagedList<MyConversationDto>(
    (page) => this.conversationsClient.getMine('Helper', undefined, undefined, page, ORDERS_PAGE_SIZE),
    (item) => item.conversationId,
    ORDERS_PAGE_SIZE,
  );

  /** Lista wybranej roli – osobno wczytywana i doładowywana. */
  readonly current = computed(() => (this.role() === 'Client' ? this.mine : this.helping));
  readonly groups = computed<OrderGroupView[]>(() =>
    this.role() === 'Client'
      ? groupOrders(this.mine.items(), clientOrderGroup).map(({ key, label, items }) => ({
          key,
          label,
          count: items.length,
          announcements: items,
          conversations: [],
        }))
      : groupOrders(this.helping.items(), helperOrderGroup).map(({ key, label, items }) => ({
          key,
          label,
          count: items.length,
          announcements: [],
          conversations: items,
        })),
  );
  /** Sekcja „Zakończone” (zakończone, anulowane, wybrano innego) – domyślnie zwinięta, aktywne są na górze. */
  readonly finishedOpen = signal(false);

  constructor() {
    addIcons({ briefcaseOutline, chatbubblesOutline, chevronDown, chevronUp, handLeftOutline });

    // Nowa wiadomość / oferta / krok zlecenia (hub albo push) – lista od razu z nowym stanem.
    reloadOnActivity(
      (activity) => !!activity.conversationId,
      () => this.current().reload(),
    );
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
