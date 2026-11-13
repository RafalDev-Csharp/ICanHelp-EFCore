import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import {
  AlertController,
  IonBackButton,
  IonBadge,
  IonButton,
  IonButtons,
  IonChip,
  IonContent,
  IonFooter,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonListHeader,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  alertCircleOutline,
  callOutline,
  chatbubbleOutline,
  chatbubblesOutline,
  checkmarkDoneOutline,
  closeCircleOutline,
  cloudOfflineOutline,
  constructOutline,
  createOutline,
  flagOutline,
  heart,
  heartOutline,
  helpCircleOutline,
  hourglassOutline,
  locationOutline,
  mailOutline,
  peopleOutline,
  personCircleOutline,
  playOutline,
  pricetagOutline,
  star,
  starOutline,
  warningOutline,
} from 'ionicons/icons';
import { catchError, filter, firstValueFrom, forkJoin, of } from 'rxjs';

import type {
  AnnouncementContactDto,
  AnnouncementDetailsDto,
  MyConversationDto,
  RatingDto,
  UserPublicDto,
} from '../../../core/api/api-client';
import { AnnouncementsClient, ConversationsClient, UsersClient } from '../../../core/api/api-client';
import { errorStatus } from '../../../core/api/api-errors';
import { apiResourceUrl } from '../../../core/api/api.providers';
import { loginUrlTree } from '../../../core/auth/auth.guard';
import { AuthService } from '../../../core/auth/auth.service';
import { CategoriesService } from '../../../core/categories/categories.service';
import { openStreetMapUrl } from '../../../core/config/map';
import { NotificationsService } from '../../../core/notifications/notifications.service';
import { ToastService } from '../../../core/ui/toast.service';
import { BrandComponent } from '../../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import type { MapMarker } from '../../../shared/components/map/map.component';
import { MapComponent } from '../../../shared/components/map/map.component';
import { PricePipe } from '../../../shared/pipes/price.pipe';
import { TimeAgoPipe, parseApiDate } from '../../../shared/pipes/time-ago.pipe';
import { pluralPl } from '../../../shared/text/plural';
import type { JobAction, JobStep } from '../../offers/job-step';
import { JOB_ACTION_ICONS, JOB_ACTION_LABELS, jobStep } from '../../offers/job-step';
import { formatOfferTerm, offerStatusColor, offerStatusLabel, pendingOffer } from '../../offers/offer-labels';
import { OffersService } from '../../offers/offers.service';
import { difficultyColor, difficultyLabel, statusColor, statusLabel } from '../announcement-labels';
import { LikesService } from '../likes/likes.service';

import { negotiationsOf, ownerHistory } from './announcement-negotiations';

type LoadState = 'loading' | 'ready' | 'not-found' | 'failed';

/** Ile ostatnich opinii o właścicielu pokazujemy pod ogłoszeniem. */
const VISIBLE_RATINGS = 3;

/** Wątki jednego ogłoszenia – więcej pomocników w jednym ogłoszeniu się nie spodziewamy. */
const CONVERSATIONS_LIMIT = 50;

/** Statusy po akceptacji oferty – wtedy uczestnicy widzą swoje dane kontaktowe. */
const AFTER_ACCEPTANCE = ['Accepted', 'InProgress', 'Completed'];

/**
 * Szczegóły ogłoszenia (publiczne): zdjęcia, opis, mapa, właściciel z ocenami, serduszko. Akcje zależą od roli:
 * - gość: „Złóż ofertę” / „Zapytaj” → logowanie i powrót tutaj;
 * - pomocnik: „Zapytaj” (wątek przed ofertą = pytania i odpowiedzi), oferta osobno (okno), po przyjęciu jego oferty –
 *   kontakt, „Rozpocznij” → „Zgłoś wykonanie” (F11 D), ocena po zakończeniu;
 * - właściciel: „Edytuj” (Open), „Anuluj” (Open/Accepted), lista negocjacji i historia ogłoszenia (oferty,
 *   kontroferty, edycje), po akceptacji – kontakt, „Potwierdź wykonanie” (wyróżnione po zgłoszeniu), ocena po zakończeniu.
 * Powiadomienie o tym ogłoszeniu (`activity$`) przeładowuje ekran.
 * Akcje na samych ofertach (przyjmij, odrzuć, kontroferta, wycofaj) są w wątku – tam widać całą negocjację.
 */
@Component({
  selector: 'app-announcement-details',
  templateUrl: './announcement-details.page.html',
  styleUrls: ['./announcement-details.page.scss'],
  imports: [
    BrandComponent,
    DatePipe,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonSkeletonText,
    IonBadge,
    IonChip,
    IonText,
    IonList,
    IonListHeader,
    IonItem,
    IonIcon,
    IonLabel,
    IonNote,
    IonButton,
    IonFooter,
    EmptyStateComponent,
    MapComponent,
    PricePipe,
    TimeAgoPipe,
  ],
})
export class AnnouncementDetailsPage implements ViewWillEnter {
  private readonly announcements = inject(AnnouncementsClient);
  private readonly conversationsClient = inject(ConversationsClient);
  private readonly users = inject(UsersClient);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly alerts = inject(AlertController);
  private readonly likes = inject(LikesService);
  private readonly offers = inject(OffersService);
  private readonly notifications = inject(NotificationsService);
  protected readonly auth = inject(AuthService);
  protected readonly categories = inject(CategoriesService);

  /** Z adresu `/announcements/:announcementId` (`withComponentInputBinding`). */
  readonly announcementId = input.required<string>();

  protected readonly state = signal<LoadState>('loading');
  protected readonly announcement = signal<AnnouncementDetailsDto | null>(null);
  protected readonly owner = signal<UserPublicDto | null>(null);
  protected readonly ownerRatings = signal<RatingDto[]>([]);
  /** Wątki zalogowanego w tym ogłoszeniu (właściciel – wszystkie, pomocnik – jego jeden). */
  protected readonly conversations = signal<MyConversationDto[]>([]);
  /** Dane drugiej strony – dopiero po akceptacji, tylko dla uczestników. */
  protected readonly contact = signal<AnnouncementContactDto | null>(null);
  /** Czy zalogowany już ocenił (po `Completed`). */
  protected readonly rated = signal(false);
  /** Trwa akcja (oferta, start, zakończenie, ocena) – przyciski nieaktywne. */
  protected readonly busy = signal(false);

  protected readonly parseApiDate = parseApiDate;
  protected readonly difficultyLabel = difficultyLabel;
  protected readonly difficultyColor = difficultyColor;
  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;
  protected readonly offerStatusLabel = offerStatusLabel;
  protected readonly offerStatusColor = offerStatusColor;
  protected readonly formatOfferTerm = formatOfferTerm;
  protected readonly jobLabels = JOB_ACTION_LABELS;
  protected readonly jobIcons = JOB_ACTION_ICONS;

  protected readonly images = computed(() =>
    (this.announcement()?.images ?? []).map((image) => ({ id: image.id, url: apiResourceUrl(image.url) ?? '' })),
  );
  protected readonly category = computed(() => {
    const announcement = this.announcement();
    return announcement
      ? [this.categories.name(announcement.categoryId), this.categories.name(announcement.subcategoryId)]
          .filter(Boolean)
          .join(' · ')
      : '';
  });
  /** „Brak ofert” / „1 oferta” / „3 oferty” – etykieta pod tytułem. */
  protected readonly offersLabel = computed(() => {
    const count = this.announcement()?.helperCount ?? 0;
    return count === 0 ? 'Brak ofert' : `${count} ${pluralPl(count, 'oferta', 'oferty', 'ofert')}`;
  });
  protected readonly marker = computed<MapMarker[]>(() => {
    const announcement = this.announcement();
    return announcement ? [{ id: announcement.id, ...announcement.location }] : [];
  });
  protected readonly mapLink = computed(() => {
    const announcement = this.announcement();
    return announcement ? openStreetMapUrl(announcement.location) : '';
  });
  protected readonly isOwner = computed(() => {
    const user = this.auth.currentUser();
    return user !== null && user.id === this.announcement()?.ownerId;
  });
  protected readonly status = computed(() => this.announcement()?.status ?? '');
  protected readonly isOpen = computed(() => this.status() === 'Open');
  /** Pomocnik, którego ofertę przyjęto (pomocnik widzi tylko swoje oferty, więc wystarczy szukać przyjętej). */
  protected readonly isAcceptedHelper = computed(() => {
    const announcement = this.announcement();
    const userId = this.auth.currentUser()?.id;
    return (
      !!announcement?.acceptedOfferId &&
      !!userId &&
      announcement.offers.some((offer) => offer.id === announcement.acceptedOfferId && offer.helperId === userId)
    );
  });
  protected readonly isParticipant = computed(() => this.isOwner() || this.isAcceptedHelper());
  protected readonly afterAcceptance = computed(() => AFTER_ACCEPTANCE.includes(this.status()));
  protected readonly negotiations = computed(() =>
    negotiationsOf(this.announcement()?.offers ?? [], this.conversations(), this.auth.currentUser()?.id),
  );
  /** Pomocnik: jego oczekująca oferta (zamiast „Złóż ofertę” – „Twoja oferta”). */
  protected readonly myPendingOffer = computed(() =>
    this.isOwner() ? undefined : pendingOffer(this.announcement()?.offers ?? []),
  );
  /** Pomocnik: jego własna oczekująca oferta, która jest nieaktualna (ogłoszenie edytowano) – do aktualizacji. */
  protected readonly myOutdatedOffer = computed(() => {
    const offer = this.myPendingOffer();
    return offer?.isOutdated && offer.proposedBy === 'Helper' ? offer : undefined;
  });
  /** Krok realizacji dla uczestnika przyjętego zlecenia (F11 D – jak w wątku). */
  protected readonly job = computed<JobStep>(() =>
    jobStep(this.status(), this.isOwner() ? 'Client' : 'Helper', this.announcement()?.doneReportedAt),
  );
  /** Pomocnik: jego wątek w tym ogłoszeniu. */
  protected readonly myConversationId = computed(() =>
    this.isOwner() ? undefined : this.conversations()[0]?.conversationId,
  );
  /** Właściciel: oferty, kontroferty i edycje od najnowszych. */
  protected readonly history = computed(() => {
    const announcement = this.announcement();
    if (!announcement || !this.isOwner()) {
      return [];
    }
    const names = new Map(this.negotiations().map((negotiation) => [negotiation.helperId, negotiation.userName]));
    return ownerHistory(announcement, names);
  });
  protected readonly isLiked = computed(() => {
    const announcement = this.announcement();
    return announcement !== null && this.auth.isAuthenticated() && this.likes.isLiked(announcement.id);
  });
  protected readonly liking = signal(false);
  protected readonly cancelling = signal(false);
  protected readonly ratingSummary = computed(() => {
    const ratings = this.ownerRatings();
    if (ratings.length === 0) {
      return null;
    }
    const average = ratings.reduce((sum, rating) => sum + rating.rate, 0) / ratings.length;
    return {
      average: average.toLocaleString('pl-PL', { minimumFractionDigits: 1, maximumFractionDigits: 1 }),
      count: `${ratings.length} ${pluralPl(ratings.length, 'ocena', 'oceny', 'ocen')}`,
    };
  });
  /** Najnowsze opinie z komentarzem. */
  protected readonly recentReviews = computed(() =>
    this.ownerRatings()
      .filter((rating) => !!rating.comment)
      .sort((a, b) => parseApiDate(b.createdAt).getTime() - parseApiDate(a.createdAt).getTime())
      .slice(0, VISIBLE_RATINGS),
  );

  constructor() {
    addIcons({
      alertCircleOutline,
      callOutline,
      chatbubbleOutline,
      chatbubblesOutline,
      checkmarkDoneOutline,
      closeCircleOutline,
      cloudOfflineOutline,
      constructOutline,
      createOutline,
      flagOutline,
      heart,
      heartOutline,
      helpCircleOutline,
      hourglassOutline,
      locationOutline,
      mailOutline,
      peopleOutline,
      personCircleOutline,
      playOutline,
      pricetagOutline,
      star,
      starOutline,
      warningOutline,
    });

    // F11 D: powiadomienie (hub albo push) o tym ogłoszeniu – np. pomocnik zgłosił wykonanie, druga strona zmieniła
    // ofertę – szczegóły przeładowane od razu.
    this.notifications.activity$
      .pipe(
        filter((activity) => activity.announcementId === this.announcementId()),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.load());
  }

  /** Przy każdym wejściu (także po powrocie z logowania – zalogowany może widzieć swoje oferty). */
  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    // Stan serduszka (raz na sesję; potem `sync` z `isLiked` ogłoszenia).
    this.likes.load().subscribe({ error: () => undefined });
    if (this.auth.isAuthenticated() && !this.auth.currentUser()) {
      // Rola (właściciel / pomocnik) zależy od użytkownika – ogłoszenie wczytujemy, gdy już go znamy.
      this.auth.loadCurrentUser().subscribe({ complete: () => this.load(), error: () => this.load() });
      return;
    }
    this.load();
  }

  refresh(event: Event): void {
    this.load(() => void (event.target as unknown as { complete(): Promise<void> }).complete());
  }

  load(done?: () => void): void {
    this.announcements.getById(this.announcementId()).subscribe({
      next: (announcement) => {
        const ownerChanged = this.announcement()?.ownerId !== announcement.ownerId;
        this.announcement.set(announcement);
        this.likes.sync(announcement.id, announcement.isLiked);
        this.state.set('ready');
        if (ownerChanged) {
          this.loadOwner(announcement.ownerId);
        }
        this.loadNegotiation(announcement);
        done?.();
      },
      error: (error: unknown) => {
        if (errorStatus(error) === 404) {
          this.announcement.set(null);
          this.state.set('not-found');
        } else if (!this.announcement()) {
          this.state.set('failed');
        }
        done?.();
      },
    });
  }

  /**
   * „Złóż ofertę”: niezalogowany → logowanie i powrót; pomocnik z własną nieaktualną ofertą → okno aktualizacji
   * (`POST …/revise`); z inną oczekującą ofertą → jego wątek (tam kontroferty);
   * inaczej okno oferty → `POST …/offers` → wątek (oferta zakłada go sama).
   */
  async makeOffer(): Promise<void> {
    const announcement = this.announcement();
    if (!announcement || this.busy() || !this.requireSignIn()) {
      return;
    }
    const outdated = this.myOutdatedOffer();
    if (outdated) {
      this.busy.set(true);
      try {
        if (await this.offers.revise(outdated)) {
          this.load();
        }
      } finally {
        this.busy.set(false);
      }
      return;
    }
    if (this.myPendingOffer()) {
      await this.writeMessage();
      return;
    }
    this.busy.set(true);
    try {
      const offerId = await this.offers.make(announcement.id, announcement.budget.amount);
      if (offerId) {
        await this.openOwnConversation(announcement.id);
      }
    } finally {
      this.busy.set(false);
    }
  }

  /** „Zapytaj” / „Rozmowa” (pomocnik): istniejący wątek albo nowy (`POST …/conversations` – istniał → ten sam id). */
  async writeMessage(): Promise<void> {
    const announcement = this.announcement();
    if (!announcement || !this.requireSignIn()) {
      return;
    }
    const existing = this.myConversationId();
    if (existing) {
      void this.router.navigate(['/conversations', existing]);
      return;
    }
    await this.openOwnConversation(announcement.id);
  }

  /** Właściciel: wątek z pomocnikiem (z listy negocjacji albo przyjętym – „Rozmowa” w stopce). */
  openConversation(conversationId: string | undefined): void {
    if (conversationId) {
      void this.router.navigate(['/conversations', conversationId]);
    }
  }

  /** Wątek przyjętego pomocnika – dla obu stron po akceptacji. */
  openAcceptedConversation(): void {
    if (this.isOwner()) {
      const acceptedOfferId = this.announcement()?.acceptedOfferId;
      const helperId = this.announcement()?.offers.find((offer) => offer.id === acceptedOfferId)?.helperId;
      this.openConversation(this.negotiations().find((n) => n.helperId === helperId)?.conversationId);
    } else {
      this.openConversation(this.myConversationId());
    }
  }

  /** „Rozpocznij” – tylko przyjęty pomocnik (Accepted → InProgress). */
  async start(): Promise<void> {
    await this.lifecycle((id) => this.offers.start(id));
  }

  /** „Zgłoś wykonanie” – przyjęty pomocnik (InProgress, raz). */
  async reportDone(): Promise<void> {
    await this.lifecycle((id) => this.offers.reportDone(id));
  }

  /** „Potwierdź wykonanie” – tylko właściciel (Accepted / InProgress → Completed), potem obie strony mogą się ocenić. */
  async complete(): Promise<void> {
    await this.lifecycle((id) => this.offers.complete(id));
  }

  /** Przycisk kroku zlecenia w stopce (`job().action`). */
  async runJobAction(): Promise<void> {
    const actions: Record<JobAction, () => Promise<void>> = {
      start: () => this.start(),
      report: () => this.reportDone(),
      complete: () => this.complete(),
    };
    const action = this.job().action;
    if (action) {
      await actions[action]();
    }
  }

  /** Ocena drugiej strony po zakończeniu. */
  async rate(): Promise<void> {
    const announcement = this.announcement();
    if (!announcement || this.busy()) {
      return;
    }
    const other = this.isOwner() ? this.contact()?.fullName : this.owner()?.fullName;
    this.busy.set(true);
    try {
      if (await this.offers.rate(announcement.id, other ?? '')) {
        this.rated.set(true);
      }
    } finally {
      this.busy.set(false);
      // Także po 409 `AlreadyRated` (np. ocena z innego urządzenia) – stan z serwera.
      this.load();
    }
  }

  /** Serduszko: niezalogowany → logowanie i powrót. */
  toggleLike(): void {
    const announcement = this.announcement();
    if (!announcement || this.liking() || !this.requireSignIn()) {
      return;
    }
    this.liking.set(true);
    this.likes.toggle(announcement.id).subscribe({
      next: (liked) => {
        this.liking.set(false);
        void this.toast.info(liked ? 'Dodano do polubionych.' : 'Usunięto z polubionych.');
      },
      error: () => this.liking.set(false),
    });
  }

  edit(): void {
    void this.router.navigate(['/announcements', this.announcementId(), 'edit']);
  }

  /** Anulowanie (właściciel) – po potwierdzeniu, bo nie da się go cofnąć. */
  async cancel(): Promise<void> {
    const announcement = this.announcement();
    if (!announcement || this.cancelling()) {
      return;
    }
    const accepted = announcement.status === 'Accepted';
    const alert = await this.alerts.create({
      header: 'Anulować ogłoszenie?',
      message: accepted
        ? 'Przyjąłeś już ofertę – pomocnik dostanie informację, że zlecenie jest odwołane. Tego nie da się cofnąć.'
        : 'Ogłoszenie zniknie z listy, a oczekujące oferty przepadną. Tego nie da się cofnąć.',
      buttons: [
        { text: 'Nie', role: 'cancel' },
        { text: 'Anuluj ogłoszenie', role: 'destructive', handler: () => this.confirmCancel(announcement.id) },
      ],
    });
    await alert.present();
  }

  private confirmCancel(announcementId: string): void {
    this.cancelling.set(true);
    this.announcements.cancel(announcementId).subscribe({
      next: () => {
        this.cancelling.set(false);
        void this.toast.success('Ogłoszenie anulowane.');
        this.load();
      },
      // 409 (np. zlecenie już w realizacji) – komunikat z interceptora; pokazujemy aktualny stan.
      error: () => {
        this.cancelling.set(false);
        this.load();
      },
    });
  }

  /** Potwierdzenie, API i komunikat – w `OffersService` (wspólne z wątkiem); potem stan z serwera (także po 409). */
  private async lifecycle(action: (announcementId: string) => Promise<boolean>): Promise<void> {
    const announcement = this.announcement();
    if (!announcement || this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await action(announcement.id);
    } finally {
      this.busy.set(false);
      this.load();
    }
  }

  private async openOwnConversation(announcementId: string): Promise<void> {
    try {
      const { id } = await firstValueFrom(this.announcements.postConversation(announcementId));
      void this.router.navigate(['/conversations', id]);
    } catch {
      // 409 (np. ogłoszenie właśnie zamknięte) – komunikat z interceptora, pokazujemy aktualny stan.
      this.load();
    }
  }

  /**
   * Dane negocjacji zalogowanego: jego wątki w tym ogłoszeniu, kontakt (po akceptacji, uczestnicy) i czy już ocenił
   * (po zakończeniu). Dodatki – błąd nie psuje ekranu.
   */
  private loadNegotiation(announcement: AnnouncementDetailsDto): void {
    if (!this.auth.isAuthenticated()) {
      this.conversations.set([]);
      return;
    }
    this.conversationsClient
      .getMine(undefined, undefined, announcement.id, 1, CONVERSATIONS_LIMIT)
      .pipe(catchError(() => of<MyConversationDto[]>([])))
      .subscribe((conversations) => this.conversations.set(conversations));

    const userId = this.auth.currentUser()?.id;
    const participant =
      userId !== undefined &&
      (userId === announcement.ownerId ||
        announcement.offers.some((offer) => offer.id === announcement.acceptedOfferId && offer.helperId === userId));
    if (!participant || !AFTER_ACCEPTANCE.includes(announcement.status)) {
      this.contact.set(null);
      this.rated.set(false);
      return;
    }
    this.announcements
      .getContact(announcement.id)
      .pipe(catchError(() => of(null)))
      .subscribe((contact) => this.contact.set(contact));
    if (announcement.status === 'Completed') {
      this.announcements
        .getRatings(announcement.id)
        .pipe(catchError(() => of<RatingDto[]>([])))
        .subscribe((ratings) => this.rated.set(ratings.some((rating) => rating.raterId === userId)));
    }
  }

  private requireSignIn(): boolean {
    if (this.auth.isAuthenticated()) {
      return true;
    }
    void this.router.navigateByUrl(loginUrlTree(this.router, this.router.url));
    return false;
  }

  /** Właściciel i jego oceny – dodatki: błąd nie psuje ekranu (komunikat pokazał interceptor). */
  private loadOwner(ownerId: string): void {
    this.owner.set(null);
    this.ownerRatings.set([]);
    forkJoin({
      owner: this.users.getById(ownerId).pipe(catchError(() => of(null))),
      ratings: this.users.getRatings(ownerId).pipe(catchError(() => of<RatingDto[]>([]))),
    }).subscribe(({ owner, ratings }) => {
      this.owner.set(owner);
      this.ownerRatings.set(ratings);
    });
  }
}
