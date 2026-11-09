import { Component, computed, inject, input, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonBadge,
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonRefresher,
  IonRefresherContent,
  IonSpinner,
  IonTextarea,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter, ViewWillLeave } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  alertCircleOutline,
  cloudOfflineOutline,
  createOutline,
  lockClosedOutline,
  pricetagOutline,
  send,
  warningOutline,
} from 'ionicons/icons';
import { catchError, filter, of } from 'rxjs';

import type { ConversationDto, OfferDto } from '../../core/api/api-client';
import { ConversationsClient, UsersClient } from '../../core/api/api-client';
import { errorStatus } from '../../core/api/api-errors';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PricePipe } from '../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { statusColor, statusLabel } from '../announcements/announcement-labels';
import { OFFER_RULES } from '../offers/offer-form';
import { formatOfferTerm, offerStatusColor, offerStatusLabel, partyLabel, pendingOffer } from '../offers/offer-labels';
import { OffersService } from '../offers/offers.service';

import { offersOf, toTimelineItems } from './conversation-timeline';

type LoadState = 'loading' | 'ready' | 'not-found' | 'failed';

/**
 * Wątek negocjacji (`/conversations/:conversationId`, tylko uczestnicy): wiadomości, oferty z akcjami (przyjmij /
 * odrzuć / kontroferta / wycofaj – zgodnie z rolą) i zmiany ogłoszenia, od najstarszych. Wejście oznacza wątek jako
 * przeczytany (razem z jego powiadomieniami). Pisać można, dopóki API pozwala (`canWrite`) – potem wątek jest tylko
 * do odczytu. Nowa wiadomość / oferta w tym wątku (powiadomienie z huba SignalR) – wątek przeładowany od razu.
 */
@Component({
  selector: 'app-conversation',
  templateUrl: './conversation.page.html',
  styleUrls: ['./conversation.page.scss'],
  imports: [
    FormsModule,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonSpinner,
    IonItem,
    IonLabel,
    IonBadge,
    IonIcon,
    IonButton,
    IonFooter,
    IonTextarea,
    EmptyStateComponent,
    PricePipe,
    TimeAgoPipe,
  ],
})
export class ConversationPage implements ViewWillEnter, ViewWillLeave {
  private readonly conversations = inject(ConversationsClient);
  private readonly users = inject(UsersClient);
  private readonly offers = inject(OffersService);
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(NotificationsService);
  private readonly content = viewChild(IonContent);

  /** Z adresu `/conversations/:conversationId` (`withComponentInputBinding`). */
  readonly conversationId = input.required<string>();

  readonly state = signal<LoadState>('loading');
  readonly conversation = signal<ConversationDto | null>(null);
  /** Imię i nazwisko drugiej strony (nagłówek); do czasu wczytania – rola. */
  readonly otherName = signal('');
  readonly busy = signal(false);
  draft = '';

  protected readonly maxMessage = OFFER_RULES.message.max;
  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;
  protected readonly offerStatusLabel = offerStatusLabel;
  protected readonly offerStatusColor = offerStatusColor;
  protected readonly partyLabel = partyLabel;
  protected readonly formatOfferTerm = formatOfferTerm;

  readonly items = computed(() => {
    const conversation = this.conversation();
    return conversation ? toTimelineItems(conversation, this.auth.currentUser()?.id) : [];
  });
  readonly title = computed(() => {
    const conversation = this.conversation();
    if (!conversation) {
      return 'Rozmowa';
    }
    return this.otherName() || partyLabel(conversation.myRole === 'Client' ? 'Helper' : 'Client');
  });
  /** Pomocnik w otwartym ogłoszeniu bez oczekującej oferty może złożyć nową (np. po odrzuceniu). */
  readonly canMakeOffer = computed(() => {
    const conversation = this.conversation();
    return (
      !!conversation &&
      conversation.myRole === 'Helper' &&
      conversation.announcementStatus === 'Open' &&
      !pendingOffer(offersOf(conversation))
    );
  });

  /** Dlaczego nie można pisać (`canWrite: false`) – wg reguły `Announcement.AllowsConversationWith`. */
  readonly readOnlyReason = computed(() => {
    switch (this.conversation()?.announcementStatus) {
      case 'Accepted':
      case 'InProgress':
        return 'Zleceniodawca przyjął inną ofertę – rozmowa jest tylko do odczytu.';
      case 'Completed':
        return 'Zlecenie zakończone – rozmowa jest tylko do odczytu. Ocenę wystawisz w ogłoszeniu.';
      case 'Cancelled':
        return 'Ogłoszenie anulowane – rozmowa jest tylko do odczytu.';
      default:
        return 'Rozmowa jest tylko do odczytu.';
    }
  });

  constructor() {
    addIcons({
      alertCircleOutline,
      cloudOfflineOutline,
      createOutline,
      lockClosedOutline,
      pricetagOutline,
      send,
      warningOutline,
    });

    this.notifications.received$
      .pipe(
        filter((notification) => notification.conversationId === this.conversationId()),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.load());
  }

  ionViewWillEnter(): void {
    if (!this.auth.currentUser()) {
      this.auth.loadCurrentUser().subscribe({ error: () => undefined });
    }
    this.notifications.openConversation(this.conversationId());
    this.load();
  }

  ionViewWillLeave(): void {
    this.notifications.closeConversation(this.conversationId());
  }

  refresh(event: Event): void {
    this.load(() => void (event.target as unknown as { complete(): Promise<void> }).complete());
  }

  load(done?: () => void): void {
    this.conversations.getById(this.conversationId()).subscribe({
      next: (conversation) => {
        const first = this.conversation()?.id !== conversation.id;
        this.conversation.set(conversation);
        this.state.set('ready');
        if (first) {
          this.loadOtherName(conversation);
        }
        if (conversation.unreadCount > 0) {
          this.conversations.markAsRead(conversation.id).subscribe({ error: () => undefined });
        }
        this.scrollToBottom();
        done?.();
      },
      error: (error: unknown) => {
        if (errorStatus(error) === 404) {
          this.conversation.set(null);
          this.state.set('not-found');
        } else if (!this.conversation()) {
          this.state.set('failed');
        }
        done?.();
      },
    });
  }

  /** Wysłanie wiadomości (2–500 znaków); po sukcesie wątek z serwera (z godziną i kolejnością z API). */
  send(): void {
    const text = this.draft.trim();
    if (text.length < OFFER_RULES.message.min || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.conversations.postMessage(this.conversationId(), { text }).subscribe({
      next: () => {
        this.busy.set(false);
        this.draft = '';
        this.load();
      },
      // 409 – np. ogłoszenie właśnie przyjęło innego pomocnika; pokazujemy aktualny stan, treść zostaje w polu.
      error: () => {
        this.busy.set(false);
        this.load();
      },
    });
  }

  async makeOffer(): Promise<void> {
    const conversation = this.conversation();
    if (!conversation) {
      return;
    }
    const last = offersOf(conversation).at(-1);
    await this.act(() => this.offers.make(conversation.announcementId, last?.price.amount).then((id) => id !== null));
  }

  async accept(offer: OfferDto): Promise<void> {
    await this.act(() => this.offers.accept(offer));
  }

  async reject(offer: OfferDto): Promise<void> {
    await this.act(() => this.offers.reject(offer));
  }

  async counter(offer: OfferDto): Promise<void> {
    await this.act(() => this.offers.counter(offer));
  }

  async withdraw(offer: OfferDto): Promise<void> {
    await this.act(() => this.offers.withdraw(offer));
  }

  /** Jedna akcja naraz; po zmianie (albo błędzie 409 – stan mógł się zmienić) przeładowanie wątku. */
  private async act(action: () => Promise<boolean>): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await action();
    } finally {
      this.busy.set(false);
      this.load();
    }
  }

  private loadOtherName(conversation: ConversationDto): void {
    const otherId = conversation.myRole === 'Client' ? conversation.helperId : conversation.clientId;
    this.users
      .getById(otherId)
      .pipe(catchError(() => of(null)))
      .subscribe((user) => this.otherName.set(user?.fullName ?? ''));
  }

  private scrollToBottom(): void {
    // Po narysowaniu nowych wpisów (następna klatka).
    setTimeout(() => void this.content()?.scrollToBottom(0));
  }
}
