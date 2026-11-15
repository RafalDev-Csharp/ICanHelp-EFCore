import { Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  AlertController,
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonListHeader,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import {
  alertCircleOutline,
  callOutline,
  chatbubbleEllipsesOutline,
  chatbubbleOutline,
  checkmarkDoneOutline,
  closeCircleOutline,
  copyOutline,
  createOutline,
  exitOutline,
  eyeOutline,
  heart,
  heartOutline,
  helpCircleOutline,
  mailOutline,
  personCircleOutline,
  starOutline,
  stopCircleOutline,
} from 'ionicons/icons';
import { firstValueFrom } from 'rxjs';

import { AnnouncementsClient } from '../../../core/api/api-client';
import { loginUrlTree } from '../../../core/auth/auth.guard';
import { AuthService } from '../../../core/auth/auth.service';
import { CategoriesService } from '../../../core/categories/categories.service';
import { reloadOnActivity } from '../../../core/notifications/reload-on-activity';
import { ToastService } from '../../../core/ui/toast.service';
import { BrandComponent } from '../../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../../shared/components/load-state/load-failed.component';
import { runExclusive } from '../../../shared/data/exclusive';
import { completeIonEvent } from '../../../shared/data/ion-events';
import { PricePipe } from '../../../shared/pipes/price.pipe';
import { INTERRUPTED_NOTE } from '../../offers/job-exit';
import { jobStepView, registerJobStepIcons } from '../../offers/job-step';
import { JobService } from '../../offers/job.service';
import { oppositeParty, partyLabel } from '../../offers/offer-labels';
import { OffersService } from '../../offers/offers.service';
import { statusLabel } from '../announcement-labels';
import { LikesService } from '../likes/likes.service';

import { AnnouncementContentComponent } from './announcement-content.component';
import { AnnouncementDetailsStore } from './announcement-details.store';
import type { FooterMode } from './announcement-footer';
import { footerMode } from './announcement-footer';
import { AnnouncementNegotiationsComponent } from './announcement-negotiations.component';
import { AnnouncementOwnerComponent } from './announcement-owner.component';

/**
 * Szczegóły ogłoszenia (publiczne): treść (`app-announcement-content`), właściciel z ocenami, kontakt po akceptacji,
 * negocjacje i historia, serduszko. Dane i rola czytającego – `AnnouncementDetailsStore`; tu akcje i stopka, która
 * zależy od roli (`footerMode`):
 * - gość: „Złóż ofertę” / „Zapytaj” → logowanie i powrót tutaj;
 * - pomocnik: „Zapytaj” (wątek przed ofertą = pytania i odpowiedzi), oferta osobno (okno), po przyjęciu jego oferty –
 *   kontakt, „Rozpocznij” → „Zgłoś wykonanie” (F11 D), ocena po zakończeniu;
 * - właściciel: „Edytuj” (Open), „Anuluj” (Open/Accepted), po akceptacji – kontakt, „Potwierdź wykonanie” (wyróżnione
 *   po zgłoszeniu), ocena po zakończeniu;
 * - F11 E/G: obie strony – „Zrezygnuj” (Accepted) / „Przerwij zlecenie” (InProgress) z powodem; po przerwaniu bez ocen –
 *   druga strona „Dodaj swoje stanowisko”, notka o rozliczeniu i u właściciela „Wystaw ponownie” (`/add?copyOf=`).
 * Powiadomienie o tym ogłoszeniu (`activity$`) przeładowuje ekran.
 * Akcje na samych ofertach (przyjmij, odrzuć, kontroferta, wycofaj) są w wątku – tam widać całą negocjację.
 */
@Component({
  selector: 'app-announcement-details',
  templateUrl: './announcement-details.page.html',
  styleUrls: ['./announcement-details.page.scss'],
  providers: [AnnouncementDetailsStore],
  imports: [
    BrandComponent,
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
    IonList,
    IonListHeader,
    IonItem,
    IonIcon,
    IonLabel,
    IonButton,
    IonFooter,
    EmptyStateComponent,
    LoadFailedComponent,
    AnnouncementContentComponent,
    AnnouncementOwnerComponent,
    AnnouncementNegotiationsComponent,
    PricePipe,
  ],
})
export class AnnouncementDetailsPage implements ViewWillEnter {
  private readonly announcements = inject(AnnouncementsClient);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly alerts = inject(AlertController);
  private readonly likes = inject(LikesService);
  private readonly offers = inject(OffersService);
  private readonly jobs = inject(JobService);
  private readonly auth = inject(AuthService);
  private readonly categories = inject(CategoriesService);
  protected readonly store = inject(AnnouncementDetailsStore);

  /** Z adresu `/announcements/:announcementId` (`withComponentInputBinding`). */
  readonly announcementId = input.required<string>();

  /** Trwa akcja (oferta, krok zlecenia, ocena) – przyciski nieaktywne. */
  protected readonly busy = signal(false);
  protected readonly liking = signal(false);
  protected readonly cancelling = signal(false);

  protected readonly statusLabel = statusLabel;

  protected readonly isLiked = computed(() => {
    const announcement = this.store.announcement();
    return announcement !== null && this.auth.isAuthenticated() && this.likes.isLiked(announcement.id);
  });
  /** Która wersja stopki (rola czytającego × status). */
  protected readonly footer = computed<FooterMode>(() =>
    footerMode({
      status: this.store.status(),
      isOwner: this.store.isOwner(),
      isParticipant: this.store.isParticipant(),
      interrupted: this.store.interruption() !== null,
      abandoned: !!this.store.myAbandonment(),
    }),
  );
  /** Krok realizacji w stopce uczestnika (notka, przycisk kroku, wyjście). */
  protected readonly job = computed(() => jobStepView(this.store.job()));
  /** „Pomocnik przerwał zlecenie – Brak kontaktu. Ustalcie między sobą rozliczenie…” */
  protected readonly interruptionNote = computed(() => {
    const entry = this.store.interruption();
    return entry ? `${entry.title} – ${entry.reason.replace(/\.$/, '')}. ${INTERRUPTED_NOTE}` : '';
  });

  constructor() {
    addIcons({
      alertCircleOutline,
      callOutline,
      chatbubbleEllipsesOutline,
      chatbubbleOutline,
      checkmarkDoneOutline,
      closeCircleOutline,
      copyOutline,
      createOutline,
      exitOutline,
      eyeOutline,
      heart,
      heartOutline,
      helpCircleOutline,
      mailOutline,
      personCircleOutline,
      starOutline,
      stopCircleOutline,
    });
    registerJobStepIcons();

    // F11 D: powiadomienie (hub albo push) o tym ogłoszeniu – np. pomocnik zgłosił wykonanie, druga strona zmieniła
    // ofertę – szczegóły przeładowane od razu.
    reloadOnActivity(
      (activity) => activity.announcementId === this.announcementId(),
      () => this.load(),
    );
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
    this.load(() => completeIonEvent(event));
  }

  load(done?: () => void): void {
    this.store.load(this.announcementId(), done);
  }

  /**
   * „Złóż ofertę”: niezalogowany → logowanie i powrót; pomocnik z ofertą sprzed edycji ogłoszenia → podgląd zmian
   * i decyzja (`OffersService.reviewChanges`: podtrzymaj / zmień / wycofaj); z inną oczekującą ofertą → jego wątek (tam kontroferty);
   * inaczej okno oferty → `POST …/offers` → wątek (oferta zakłada go sama).
   */
  async makeOffer(): Promise<void> {
    const announcement = this.store.announcement();
    if (!announcement || this.busy() || !this.requireSignIn()) {
      return;
    }
    const outdated = this.store.myOutdatedOffer();
    if (outdated) {
      await runExclusive(this.busy, async () => {
        if (await this.offers.reviewChanges(outdated)) {
          this.load();
        }
      });
      return;
    }
    if (this.store.myPendingOffer()) {
      await this.writeMessage();
      return;
    }
    await runExclusive(this.busy, async () => {
      const offerId = await this.offers.make(announcement.id, announcement.budget.amount);
      if (offerId) {
        await this.openOwnConversation(announcement.id);
      }
    });
  }

  /** „Zapytaj” / „Rozmowa” (pomocnik): istniejący wątek albo nowy (`POST …/conversations` – istniał → ten sam id). */
  async writeMessage(): Promise<void> {
    const announcement = this.store.announcement();
    if (!announcement || !this.requireSignIn()) {
      return;
    }
    const existing = this.store.myConversationId();
    if (existing) {
      this.openConversation(existing);
      return;
    }
    await this.openOwnConversation(announcement.id);
  }

  /** Wątek z pomocnikiem: z listy negocjacji albo – po akceptacji – przyjętego („Rozmowa” w stopce). */
  openConversation(conversationId: string | undefined): void {
    if (conversationId) {
      void this.router.navigate(['/conversations', conversationId]);
    }
  }

  /** Przycisk kroku zlecenia w stopce: „Rozpocznij” / „Zgłoś wykonanie” (pomocnik), „Potwierdź wykonanie” (właściciel). */
  async runJobAction(): Promise<void> {
    const action = this.store.job().action;
    if (action) {
      await this.lifecycle((id) => this.jobs.run(id, action));
    }
  }

  /** F11 E: „Zrezygnuj” / „Przerwij zlecenie” – okno z powodem (wspólne z wątkiem). */
  async exitJob(): Promise<void> {
    const exit = this.store.job().exit;
    if (exit) {
      const otherSide = partyLabel(oppositeParty(this.store.myRole()));
      await this.lifecycle((id) => this.jobs.exit(id, exit, otherSide));
    }
  }

  /** F11 E: „Wystaw ponownie” po przerwaniu – formularz „Dodaj” wypełniony kopią (bez zdjęć). */
  republish(): void {
    void this.router.navigate(['/add'], { queryParams: { copyOf: this.announcementId() } });
  }

  /** F11 G: „Dodaj swoje stanowisko” po przerwaniu (druga strona, raz). */
  async respondToExit(): Promise<void> {
    const number = this.store.interruption()?.exit.number;
    if (number !== undefined) {
      await this.lifecycle((id) => this.jobs.respondToExit(id, number));
    }
  }

  /** Ocena drugiej strony po zakończeniu (po przerwaniu – bez ocen, F11 G). */
  async rate(): Promise<void> {
    const announcement = this.store.announcement();
    if (!announcement) {
      return;
    }
    const other = this.store.isOwner() ? this.store.contact()?.fullName : this.store.owner()?.fullName;
    await runExclusive(this.busy, async () => {
      try {
        if (await this.jobs.rate(announcement.id, other ?? '')) {
          this.store.rated.set(true);
        }
      } finally {
        // Także po 409 `AlreadyRated` (np. ocena z innego urządzenia) – stan z serwera.
        this.load();
      }
    });
  }

  /** Serduszko: niezalogowany → logowanie i powrót. */
  toggleLike(): void {
    const announcement = this.store.announcement();
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
    const announcement = this.store.announcement();
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

  /** Jedna akcja naraz (`JobService` – potwierdzenie, API, komunikat); potem stan z serwera (także po 409). */
  private async lifecycle(action: (announcementId: string) => Promise<boolean>): Promise<void> {
    const announcement = this.store.announcement();
    if (!announcement) {
      return;
    }
    await runExclusive(this.busy, async () => {
      try {
        await action(announcement.id);
      } finally {
        this.load();
      }
    });
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

  private requireSignIn(): boolean {
    if (this.auth.isAuthenticated()) {
      return true;
    }
    void this.router.navigateByUrl(loginUrlTree(this.router, this.router.url));
    return false;
  }
}
