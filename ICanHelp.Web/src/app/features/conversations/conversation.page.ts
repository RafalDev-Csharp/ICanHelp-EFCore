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
  arrowDownCircleOutline,
  checkmarkDoneOutline,
  cloudOfflineOutline,
  createOutline,
  eyeOutline,
  flagOutline,
  helpCircleOutline,
  hourglassOutline,
  imageOutline,
  lockClosedOutline,
  playOutline,
  pricetagOutline,
  send,
} from 'ionicons/icons';
import { catchError, filter, of } from 'rxjs';

import type { AnnouncementFieldChangeDto, ConversationDto, OfferDto } from '../../core/api/api-client';
import { AnnouncementsClient, ConversationsClient, UsersClient } from '../../core/api/api-client';
import { errorStatus } from '../../core/api/api-errors';
import { apiResourceUrl } from '../../core/api/api.providers';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PricePipe } from '../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { statusColor, statusLabel } from '../announcements/announcement-labels';
import type { JobAction, JobStep } from '../offers/job-step';
import { JOB_ACTION_ICONS, JOB_ACTION_LABELS, jobStep } from '../offers/job-step';
import { OFFER_RULES } from '../offers/offer-form';
import { formatDateTime, formatOfferTerm, partyLabel, pendingOffer } from '../offers/offer-labels';
import { OffersService } from '../offers/offers.service';

import type { ThreadNotice } from './conversation-timeline';
import { offersOf, threadNotice, toTimelineItems } from './conversation-timeline';

type LoadState = 'loading' | 'ready' | 'not-found' | 'failed';

/**
 * Wątek negocjacji (`/conversations/:conversationId`, tylko uczestnicy): wiadomości, oferty z akcjami (przyjmij /
 * odrzuć / kontroferta / wycofaj – zgodnie z rolą) i zmiany ogłoszenia, od najstarszych. Wejście oznacza wątek jako
 * przeczytany (razem z jego powiadomieniami). Pisać można, dopóki API pozwala (`canWrite`) – potem wątek jest tylko
 * do odczytu. Nowa wiadomość / oferta / zmiana ogłoszenia (powiadomienie z huba albo pusha – `activity$`) – wątek
 * przeładowany od razu; także przy każdym wejściu i po przeciągnięciu w dół.
 * U góry karta ogłoszenia (miniatura, tytuł, status, „Zobacz ogłoszenie”); w wątku przyjętego pomocnika – krok
 * zlecenia (`jobStep`): „Rozpocznij” / „Zgłoś wykonanie” (pomocnik), „Potwierdź wykonanie” (zleceniodawca).
 */
@Component({
  selector: 'app-conversation',
  templateUrl: './conversation.page.html',
  styleUrls: ['./conversation.page.scss'],
  imports: [
    BrandComponent,
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
  private readonly announcements = inject(AnnouncementsClient);
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
  protected readonly partyLabel = partyLabel;
  protected readonly formatOfferTerm = formatOfferTerm;
  protected readonly formatDateTime = formatDateTime;
  /** Oferta wyróżniona po „zobacz aktualną” (`notice` typu `revised`). */
  readonly highlightedOfferId = signal<string | null>(null);
  protected readonly jobLabels = JOB_ACTION_LABELS;
  protected readonly jobIcons = JOB_ACTION_ICONS;

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
  /** Wątek bez żadnej oferty = pytania i odpowiedzi przed ofertą (oferta to osobny przycisk). */
  readonly questionsOnly = computed(() => {
    const conversation = this.conversation();
    return !!conversation && offersOf(conversation).length === 0;
  });
  /** Miniatura ogłoszenia w karcie u góry (pierwsze zdjęcie); brak zdjęć albo błąd – ikona. */
  readonly thumbnailUrl = signal<string | undefined>(undefined);
  /** Wątek z przyjętym pomocnikiem – tu są akcje zlecenia (jak w szczegółach ogłoszenia). */
  readonly isAcceptedThread = computed(() => {
    const conversation = this.conversation();
    return !!conversation && offersOf(conversation).some((offer) => offer.status === 'Accepted');
  });
  /**
   * Krok realizacji w przyjętym wątku (F11 D, `jobStep`): pomocnik – „Rozpocznij” / „Zgłoś wykonanie”, zleceniodawca –
   * „Potwierdź wykonanie” (wyróżnione po zgłoszeniu) i notka, na co czeka.
   */
  readonly job = computed<JobStep | null>(() => {
    const conversation = this.conversation();
    if (!conversation || !this.isAcceptedThread()) {
      return null;
    }
    const step = jobStep(conversation.announcementStatus, conversation.myRole, conversation.announcementDoneReportedAt);
    return step.action || step.note ? step : null;
  });
  readonly jobAction = computed<JobAction | null>(() => this.job()?.action ?? null);
  /**
   * F11 F: co czeka na czytającego – „Zleceniodawca zmienił ogłoszenie – zobacz zmiany” (autor oferty sprzed edycji)
   * albo „Pomocnik zmienił ofertę – zobacz aktualną” (odbiorca zmienionej oferty). Wyróżniony przycisk nad polem.
   */
  readonly notice = computed<ThreadNotice | null>(() => {
    const conversation = this.conversation();
    return conversation ? threadNotice(conversation) : null;
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
        return 'Zlecenie zakończone – rozmowa jest tylko do odczytu. Ocenę wystawisz w Koncie → Oceny.';
      case 'Cancelled':
        return 'Ogłoszenie anulowane – rozmowa jest tylko do odczytu.';
      default:
        return 'Rozmowa jest tylko do odczytu.';
    }
  });

  constructor() {
    addIcons({
      alertCircleOutline,
      arrowDownCircleOutline,
      checkmarkDoneOutline,
      cloudOfflineOutline,
      createOutline,
      eyeOutline,
      flagOutline,
      helpCircleOutline,
      hourglassOutline,
      imageOutline,
      lockClosedOutline,
      playOutline,
      pricetagOutline,
      send,
    });

    // Powiadomienie (hub albo push) o tym wątku albo jego ogłoszeniu – np. druga strona zmieniła ofertę, pomocnik
    // zgłosił wykonanie, ogłoszenie edytowano – wątek przeładowany od razu.
    this.notifications.activity$
      .pipe(
        filter(
          (activity) =>
            activity.conversationId === this.conversationId() ||
            (!!activity.announcementId && activity.announcementId === this.conversation()?.announcementId),
        ),
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
          this.loadThumbnail(conversation.announcementId);
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

  /** Autor oczekującej oferty: nowe warunki (formularz). */
  async revise(offer: OfferDto): Promise<void> {
    await this.act(() => this.offers.revise(offer));
  }

  /** Autor oferty sprzed edycji ogłoszenia: podgląd zmian → podtrzymaj / zmień / wycofaj (`OffersService`). */
  async reviewChanges(offer: OfferDto): Promise<void> {
    await this.act(() => this.offers.reviewChanges(offer));
  }

  /** Wpis „Ogłoszenie zmienione” – sam podgląd było → jest. */
  async showChange(changes: readonly AnnouncementFieldChangeDto[]): Promise<void> {
    await this.offers.showChanges(changes);
  }

  /** Przycisk nad polem wiadomości: podgląd zmian albo przewinięcie do aktualnej (zmienionej) oferty. */
  async openNotice(notice: ThreadNotice): Promise<void> {
    if (notice.kind === 'changes') {
      await this.reviewChanges(notice.offer);
      return;
    }
    this.highlightedOfferId.set(notice.offer.id);
    document.getElementById(`offer-${notice.offer.id}`)?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
  }

  /** „Czeka na odpowiedź pomocnika / zleceniodawcy”. */
  protected partyGenitive(party: string): string {
    return party === 'Helper' ? 'pomocnika' : 'zleceniodawcy';
  }

  /** Akcja zlecenia w przyjętym wątku (potwierdzenie i komunikat – `OffersService`, wspólne ze szczegółami). */
  async runJobAction(): Promise<void> {
    const conversation = this.conversation();
    const action = this.jobAction();
    if (!conversation || !action) {
      return;
    }
    const id = conversation.announcementId;
    const run: Record<JobAction, () => Promise<boolean>> = {
      start: () => this.offers.start(id),
      report: () => this.offers.reportDone(id),
      complete: () => this.offers.complete(id),
    };
    await this.act(run[action]);
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

  /** Dodatek do karty ogłoszenia – błąd nie psuje wątku (zostaje ikona). */
  private loadThumbnail(announcementId: string): void {
    this.announcements
      .getById(announcementId)
      .pipe(catchError(() => of(null)))
      .subscribe((announcement) => this.thumbnailUrl.set(apiResourceUrl(announcement?.images[0]?.url)));
  }

  private scrollToBottom(): void {
    // Po narysowaniu nowych wpisów (następna klatka).
    setTimeout(() => void this.content()?.scrollToBottom(0));
  }
}
