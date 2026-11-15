import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, forkJoin, of } from 'rxjs';

import type {
  AnnouncementContactDto,
  AnnouncementDetailsDto,
  MyConversationDto,
  OfferParty,
  RatingDto,
  UserPublicDto,
  UserReliabilityDto,
} from '../../../core/api/api-client';
import { AnnouncementsClient, ConversationsClient, UsersClient } from '../../../core/api/api-client';
import { AuthService } from '../../../core/auth/auth.service';
import { RemoteItem } from '../../../shared/data/remote-item';
import type { JobExitView } from '../../offers/job-exit';
import { jobExitView, reliabilityLabel } from '../../offers/job-exit';
import type { JobStep } from '../../offers/job-step';
import { jobStep } from '../../offers/job-step';
import { pendingOffer } from '../../offers/offer-labels';
import { LikesService } from '../likes/likes.service';

import { negotiationsOf, ownerHistory } from './announcement-negotiations';

/** Wątki jednego ogłoszenia – więcej pomocników w jednym ogłoszeniu się nie spodziewamy. */
const CONVERSATIONS_LIMIT = 50;

/** Statusy po akceptacji oferty – wtedy uczestnicy widzą swoje dane kontaktowe (po przerwaniu też – rozliczenie). */
const AFTER_ACCEPTANCE = ['Accepted', 'InProgress', 'Completed', 'Interrupted'];

/**
 * Stan ekranu szczegółów ogłoszenia (jeden na ekran – `providers` strony): ogłoszenie, właściciel z ocenami,
 * negocjacje zalogowanego, kontakt po akceptacji i – wyliczona z tego – rola czytającego (gość / pomocnik / przyjęty
 * pomocnik / właściciel). Tu jest tylko „co wiemy”; akcje użytkownika (oferta, krok zlecenia, anulowanie) ma strona.
 * Dodatki (właściciel, wątki, kontakt, liczniki) nie psują ekranu – ich błąd oznacza po prostu brak tej części.
 */
@Injectable()
export class AnnouncementDetailsStore {
  private readonly announcements = inject(AnnouncementsClient);
  private readonly conversationsClient = inject(ConversationsClient);
  private readonly users = inject(UsersClient);
  private readonly likes = inject(LikesService);
  private readonly auth = inject(AuthService);

  private announcementId = '';
  private readonly resource = new RemoteItem<AnnouncementDetailsDto>(
    () => this.announcements.getById(this.announcementId),
    (announcement, previous) => this.loadRelated(announcement, previous),
  );

  readonly state = this.resource.state;
  readonly announcement = this.resource.value;
  readonly owner = signal<UserPublicDto | null>(null);
  readonly ownerRatings = signal<RatingDto[]>([]);
  /** Licznik rezygnacji / przerwań właściciela („1 rezygnacja”, pusty = brak). */
  readonly ownerReliability = signal('');
  /** Wątki zalogowanego w tym ogłoszeniu (właściciel – wszystkie, pomocnik – jego jeden). */
  readonly conversations = signal<MyConversationDto[]>([]);
  /** Właściciel: liczniki pomocników z listy negocjacji (helperId → tekst). */
  readonly helperReliability = signal<ReadonlyMap<string, string>>(new Map());
  /** Dane drugiej strony – dopiero po akceptacji, tylko dla uczestników. */
  readonly contact = signal<AnnouncementContactDto | null>(null);
  /** Czy zalogowany już ocenił (po `Completed`). */
  readonly rated = signal(false);

  readonly status = computed(() => this.announcement()?.status ?? '');
  readonly isOpen = computed(() => this.status() === 'Open');
  readonly isOwner = computed(() => {
    const user = this.auth.currentUser();
    return user !== null && user.id === this.announcement()?.ownerId;
  });
  /** Pomocnik, którego ofertę przyjęto (pomocnik widzi tylko swoje oferty, więc wystarczy szukać przyjętej). */
  readonly isAcceptedHelper = computed(() => {
    const announcement = this.announcement();
    const userId = this.auth.currentUser()?.id;
    return (
      !!announcement?.acceptedOfferId &&
      !!userId &&
      announcement.offers.some((offer) => offer.id === announcement.acceptedOfferId && offer.helperId === userId)
    );
  });
  readonly isParticipant = computed(() => this.isOwner() || this.isAcceptedHelper());
  /** Rola czytającego w zleceniu: właściciel = zleceniodawca, każdy inny = pomocnik. */
  readonly myRole = computed<OfferParty>(() => (this.isOwner() ? 'Client' : 'Helper'));

  /** F11 E/G: przerwanie zlecenia – kto, powód, stanowisko drugiej strony i czy czytający może je dodać (bez ocen). */
  readonly interruption = computed<JobExitView | null>(() => {
    const announcement = this.announcement();
    const exit = announcement?.jobExits?.find((e) => e.kind === 'Interrupted');
    return announcement && exit ? jobExitView(exit, this.auth.currentUser()?.id, announcement.ownerId) : null;
  });
  /**
   * F11 E: pomocnik, którego zlecenie zerwano przed rozpoczęciem (on albo zleceniodawca) – nie złoży już oferty,
   * jego wątek jest tylko do odczytu.
   */
  readonly myAbandonment = computed(() => {
    const userId = this.auth.currentUser()?.id;
    return this.isOwner() || !userId
      ? undefined
      : this.announcement()?.jobExits?.find((e) => e.kind === 'Abandoned' && e.helperId === userId);
  });

  readonly negotiations = computed(() =>
    negotiationsOf(this.announcement()?.offers ?? [], this.conversations(), this.auth.currentUser()?.id),
  );
  /** Pomocnik: jego oczekująca oferta (zamiast „Złóż ofertę” – „Twoja oferta”). */
  readonly myPendingOffer = computed(() =>
    this.isOwner() ? undefined : pendingOffer(this.announcement()?.offers ?? []),
  );
  /** Pomocnik: jego własna oczekująca oferta, która jest nieaktualna (ogłoszenie edytowano) – do aktualizacji. */
  readonly myOutdatedOffer = computed(() => {
    const offer = this.myPendingOffer();
    return offer?.isOutdated && offer.proposedBy === 'Helper' ? offer : undefined;
  });
  /** Pomocnik: jego wątek w tym ogłoszeniu. */
  readonly myConversationId = computed(() => (this.isOwner() ? undefined : this.conversations()[0]?.conversationId));
  /** Wątek przyjętego pomocnika – dla obu stron po akceptacji. */
  readonly acceptedConversationId = computed(() => {
    if (!this.isOwner()) {
      return this.myConversationId();
    }
    const announcement = this.announcement();
    const helperId = announcement?.offers.find((offer) => offer.id === announcement.acceptedOfferId)?.helperId;
    return this.negotiations().find((negotiation) => negotiation.helperId === helperId)?.conversationId;
  });
  /** Krok realizacji dla uczestnika przyjętego zlecenia (F11 D – jak w wątku). */
  readonly job = computed<JobStep>(() => jobStep(this.status(), this.myRole(), this.announcement()?.doneReportedAt));
  /** Właściciel: oferty, kontroferty i edycje od najnowszych. */
  readonly history = computed(() => {
    const announcement = this.announcement();
    if (!announcement || !this.isOwner()) {
      return [];
    }
    const names = new Map(this.negotiations().map((negotiation) => [negotiation.helperId, negotiation.userName]));
    return ownerHistory(announcement, names);
  });

  /** Ogłoszenie z adresu ekranu (i dodatki dla zalogowanego); `done` – po sukcesie i po błędzie. */
  load(announcementId: string, done?: () => void): void {
    this.announcementId = announcementId;
    this.resource.load(done);
  }

  private loadRelated(announcement: AnnouncementDetailsDto, previous: AnnouncementDetailsDto | null): void {
    this.likes.sync(announcement.id, announcement.isLiked);
    if (previous?.ownerId !== announcement.ownerId) {
      this.loadOwner(announcement.ownerId);
    }
    this.loadNegotiation(announcement);
  }

  /** Właściciel i jego oceny – dodatki: błąd nie psuje ekranu (komunikat pokazał interceptor). */
  private loadOwner(ownerId: string): void {
    this.owner.set(null);
    this.ownerRatings.set([]);
    this.ownerReliability.set('');
    forkJoin({
      owner: this.users.getById(ownerId).pipe(catchError(() => of(null))),
      ratings: this.users.getRatings(ownerId).pipe(catchError(() => of<RatingDto[]>([]))),
      reliability: this.users.getReliability(ownerId).pipe(catchError(() => of<UserReliabilityDto | null>(null))),
    }).subscribe(({ owner, ratings, reliability }) => {
      this.owner.set(owner);
      this.ownerRatings.set(ratings);
      this.ownerReliability.set(reliabilityLabel(reliability));
    });
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
      .subscribe((conversations) => {
        this.conversations.set(conversations);
        this.loadHelperReliability();
      });

    const userId = this.auth.currentUser()?.id;
    if (!this.isParticipant() || !AFTER_ACCEPTANCE.includes(announcement.status)) {
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

  /** Właściciel: liczniki rezygnacji / przerwań pomocników z listy negocjacji (dodatek – błąd = bez licznika). */
  private loadHelperReliability(): void {
    const helperIds = this.isOwner() ? this.negotiations().map((negotiation) => negotiation.helperId) : [];
    if (helperIds.length === 0) {
      this.helperReliability.set(new Map());
      return;
    }
    forkJoin(
      helperIds.map((id) => this.users.getReliability(id).pipe(catchError(() => of<UserReliabilityDto | null>(null)))),
    ).subscribe((results) =>
      this.helperReliability.set(new Map(helperIds.map((id, index) => [id, reliabilityLabel(results[index])]))),
    );
  }
}
