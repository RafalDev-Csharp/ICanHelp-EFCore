import type { Signal } from '@angular/core';
import { Component, computed, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonIcon,
  IonRefresher,
  IonRefresherContent,
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
  createOutline,
  exitOutline,
  eyeOutline,
  helpCircleOutline,
  lockClosedOutline,
  pricetagOutline,
  send,
  stopCircleOutline,
} from 'ionicons/icons';
import { catchError, of } from 'rxjs';

import type { AnnouncementFieldChangeDto, ConversationDto, OfferDto } from '../../core/api/api-client';
import { AnnouncementsClient, ConversationsClient, UsersClient } from '../../core/api/api-client';
import { apiResourceUrl } from '../../core/api/api.providers';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationsService } from '../../core/notifications/notifications.service';
import { reloadOnActivity } from '../../core/notifications/reload-on-activity';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { runExclusive } from '../../shared/data/exclusive';
import { RemoteItem } from '../../shared/data/remote-item';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { INTERRUPTED_NOTE, reliabilityLabel } from '../offers/job-exit';
import { jobStepView, registerJobStepIcons } from '../offers/job-step';
import { JobService } from '../offers/job.service';
import { OfferCardComponent } from '../offers/offer-card.component';
import { OFFER_RULES } from '../offers/offer-form';
import { formatDateTime, oppositeParty, partyLabel } from '../offers/offer-labels';
import type { OfferAction } from '../offers/offer-view';
import { OffersService } from '../offers/offers.service';

import { ConversationSubjectComponent } from './conversation-subject.component';
import {
  canMakeOffer,
  isQuestionsOnly,
  messagePlaceholder,
  otherPartyId,
  readOnlyReason,
  threadJobStep,
} from './conversation-thread';
import type { ThreadNotice } from './conversation-timeline';
import { offersOf, threadNotice, toTimelineItems } from './conversation-timeline';

/**
 * Wątek negocjacji (`/conversations/:conversationId`, tylko uczestnicy): wiadomości, oferty z akcjami (przyjmij /
 * odrzuć / kontroferta / wycofaj – zgodnie z rolą) i zmiany ogłoszenia, od najstarszych. Wejście oznacza wątek jako
 * przeczytany (razem z jego powiadomieniami). Pisać można, dopóki API pozwala (`canWrite`) – potem wątek jest tylko
 * do odczytu. Nowa wiadomość / oferta / zmiana ogłoszenia (powiadomienie z huba albo pusha – `activity$`) – wątek
 * przeładowany od razu; także przy każdym wejściu i po przeciągnięciu w dół.
 * U góry karta ogłoszenia (`app-conversation-subject`); w wątku przyjętego pomocnika – krok zlecenia (`jobStep`):
 * „Rozpocznij” / „Zgłoś wykonanie” (pomocnik), „Potwierdź wykonanie” (zleceniodawca). Reguły wątku (co wolno
 * czytającemu) – `conversation-thread.ts`, wpisy osi – `conversation-timeline.ts`.
 */
@Component({
  selector: 'app-conversation',
  templateUrl: './conversation.page.html',
  styleUrls: ['./conversation.page.scss'],
  imports: [
    BrandComponent,
    FormsModule,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonIcon,
    IonButton,
    IonFooter,
    IonTextarea,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
    ConversationSubjectComponent,
    OfferCardComponent,
    TimeAgoPipe,
  ],
})
export class ConversationPage implements ViewWillEnter, ViewWillLeave {
  private readonly conversations = inject(ConversationsClient);
  private readonly announcements = inject(AnnouncementsClient);
  private readonly users = inject(UsersClient);
  private readonly offers = inject(OffersService);
  private readonly jobs = inject(JobService);
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(NotificationsService);
  private readonly content = viewChild(IonContent);

  /** Z adresu `/conversations/:conversationId` (`withComponentInputBinding`). */
  readonly conversationId = input.required<string>();

  private readonly thread = new RemoteItem<ConversationDto>(
    () => this.conversations.getById(this.conversationId()),
    (conversation, previous) => this.onLoaded(conversation, previous),
  );
  readonly state = this.thread.state;
  readonly conversation = this.thread.value;
  /** Imię i nazwisko drugiej strony (nagłówek); do czasu wczytania – rola. */
  readonly otherName = signal('');
  /** „1 rezygnacja · 2 przerwane zlecenia” drugiej strony (pusty = brak). */
  readonly otherReliability = signal('');
  /** Miniatura ogłoszenia w karcie u góry (pierwsze zdjęcie); brak zdjęć albo błąd – ikona. */
  readonly thumbnailUrl = signal<string | undefined>(undefined);
  readonly busy = signal(false);
  /** Oferta wyróżniona po „zobacz aktualną” (`notice` typu `revised`). */
  readonly highlightedOfferId = signal<string | null>(null);
  draft = '';

  protected readonly maxMessage = OFFER_RULES.message.max;
  protected readonly formatDateTime = formatDateTime;
  protected readonly interruptedNote = INTERRUPTED_NOTE;

  readonly items = this.fromThread((conversation) => toTimelineItems(conversation, this.auth.currentUser()?.id), []);
  readonly title = computed(() => {
    const conversation = this.conversation();
    if (!conversation) {
      return 'Rozmowa';
    }
    return this.otherName() || partyLabel(oppositeParty(conversation.myRole));
  });
  /** Wątek bez żadnej oferty = pytania i odpowiedzi przed ofertą (oferta to osobny przycisk). */
  readonly questionsOnly = this.fromThread(isQuestionsOnly, false);
  /** Krok realizacji w wątku przyjętego pomocnika (`threadJobStep`) – jak w szczegółach ogłoszenia. */
  readonly job = this.fromThread(threadJobStep, null);
  protected readonly jobView = computed(() => {
    const step = this.job();
    return step ? jobStepView(step) : null;
  });
  /**
   * F11 F: co czeka na czytającego – „Zleceniodawca zmienił ogłoszenie – zobacz zmiany” (autor oferty sprzed edycji)
   * albo „Pomocnik zmienił ofertę – zobacz aktualną” (odbiorca zmienionej oferty). Wyróżniony przycisk nad polem.
   */
  readonly notice = this.fromThread(threadNotice, null);
  /** Pomocnik może złożyć (kolejną) ofertę – „Złóż ofertę” nad polem wiadomości. */
  readonly canMakeOffer = this.fromThread(canMakeOffer, false);
  /** F11 E: zlecenie przerwane w tym wątku – notka o rozliczeniu nad stopką. */
  readonly interrupted = computed(() => this.conversation()?.announcementStatus === 'Interrupted');
  /** Dlaczego nie można pisać (`canWrite: false`). */
  readonly readOnlyReason = computed(() => readOnlyReason(this.conversation()));
  protected readonly placeholder = this.fromThread(messagePlaceholder, '');

  constructor() {
    addIcons({
      alertCircleOutline,
      arrowDownCircleOutline,
      checkmarkDoneOutline,
      createOutline,
      exitOutline,
      eyeOutline,
      helpCircleOutline,
      lockClosedOutline,
      pricetagOutline,
      send,
      stopCircleOutline,
    });
    registerJobStepIcons();

    // Powiadomienie (hub albo push) o tym wątku albo jego ogłoszeniu – np. druga strona zmieniła ofertę, pomocnik
    // zgłosił wykonanie, ogłoszenie edytowano – wątek przeładowany od razu.
    reloadOnActivity(
      (activity) =>
        activity.conversationId === this.conversationId() ||
        (!!activity.announcementId && activity.announcementId === this.conversation()?.announcementId),
      () => this.load(),
    );
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
    this.thread.refresh(event);
  }

  load(): void {
    this.thread.load();
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
    await this.act(() => this.offers.make(conversation.announcementId, last?.price.amount));
  }

  /**
   * Przycisk pod ofertą (`app-offer-card`): przyjmij / odrzuć / kontroferta / wycofaj / zmień albo – dla autora oferty
   * sprzed edycji ogłoszenia – podgląd zmian i decyzja. Okno, API i komunikat – `OffersService`.
   */
  async offerAction(action: OfferAction, offer: OfferDto): Promise<void> {
    await this.act(() => this.offers[action](offer));
  }

  /** Wpis „Ogłoszenie zmienione” – sam podgląd było → jest. */
  async showChange(changes: readonly AnnouncementFieldChangeDto[]): Promise<void> {
    await this.offers.showChanges(changes);
  }

  /** Przycisk nad polem wiadomości: podgląd zmian albo przewinięcie do aktualnej (zmienionej) oferty. */
  async openNotice(notice: ThreadNotice): Promise<void> {
    if (notice.kind === 'changes') {
      await this.offerAction('reviewChanges', notice.offer);
      return;
    }
    this.highlightedOfferId.set(notice.offer.id);
    document.getElementById(`offer-${notice.offer.id}`)?.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
  }

  /** Krok zlecenia w przyjętym wątku (potwierdzenie i komunikat – `JobService`, wspólne ze szczegółami). */
  async runJobAction(): Promise<void> {
    const conversation = this.conversation();
    const action = this.job()?.action;
    if (conversation && action) {
      await this.act(() => this.jobs.run(conversation.announcementId, action));
    }
  }

  /** F11 G: „Dodaj swoje stanowisko” przy wpisie rezygnacji / przerwania (druga strona, raz). */
  async respondToExit(number: number): Promise<void> {
    const conversation = this.conversation();
    if (conversation) {
      await this.act(() => this.jobs.respondToExit(conversation.announcementId, number));
    }
  }

  /** F11 E: „Zrezygnuj” / „Przerwij zlecenie” – okno z powodem (`JobService.exit`). */
  async exitJob(): Promise<void> {
    const conversation = this.conversation();
    const exit = this.job()?.exit;
    if (conversation && exit) {
      const otherSide = partyLabel(oppositeParty(conversation.myRole));
      await this.act(() => this.jobs.exit(conversation.announcementId, exit, otherSide));
    }
  }

  /** Jedna akcja naraz; po zmianie (albo błędzie 409 – stan mógł się zmienić) przeładowanie wątku. */
  private async act(action: () => Promise<unknown>): Promise<void> {
    await runExclusive(this.busy, async () => {
      try {
        await action();
      } finally {
        this.load();
      }
    });
  }

  /** Wartość wyliczona z wczytanego wątku; `empty` – dopóki go nie ma. */
  private fromThread<T>(project: (conversation: ConversationDto) => T, empty: T): Signal<T> {
    return computed(() => {
      const conversation = this.conversation();
      return conversation ? project(conversation) : empty;
    });
  }

  private onLoaded(conversation: ConversationDto, previous: ConversationDto | null): void {
    if (previous?.id !== conversation.id) {
      this.loadOtherParty(conversation);
      this.loadThumbnail(conversation.announcementId);
    }
    if (conversation.unreadCount > 0) {
      this.conversations.markAsRead(conversation.id).subscribe({ error: () => undefined });
    }
    this.scrollToBottom();
  }

  /** Nazwa i publiczny licznik rezygnacji / przerwań drugiej strony (dodatki – błąd = bez nich). */
  private loadOtherParty(conversation: ConversationDto): void {
    const otherId = otherPartyId(conversation);
    this.users
      .getById(otherId)
      .pipe(catchError(() => of(null)))
      .subscribe((user) => this.otherName.set(user?.fullName ?? ''));
    this.users
      .getReliability(otherId)
      .pipe(catchError(() => of(null)))
      .subscribe((counts) => this.otherReliability.set(reliabilityLabel(counts)));
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
