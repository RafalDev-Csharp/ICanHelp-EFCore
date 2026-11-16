import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import type { RateAnnouncement } from '../../core/api/api-client';
import { AnnouncementsClient } from '../../core/api/api-client';
import { ApiCommands } from '../../core/api/api-commands';
import type { ConfirmOptions } from '../../core/ui/dialog.service';
import { DialogService } from '../../core/ui/dialog.service';
import { ModalService } from '../../core/ui/modal.service';

import type { JobExitAction, JobExitCommand } from './job-exit';
import { JobExitModalComponent } from './job-exit-modal.component';
import type { JobAction } from './job-step';
import { OFFER_RULES } from './offer-form';
import { RatingModalComponent } from './rating-modal.component';

/** Krok zlecenia: pytanie przed nieodwracalną akcją i komunikat po niej. */
const JOB_ACTIONS: Readonly<Record<JobAction, { question: ConfirmOptions; success: string }>> = {
  start: {
    question: {
      header: 'Rozpocząć zlecenie?',
      message: 'Zleceniodawca dostanie informację, że zaczynasz. Po wszystkim zgłosisz wykonanie.',
      confirmText: 'Rozpocznij',
    },
    success: 'Zlecenie w realizacji.',
  },
  report: {
    question: {
      header: 'Zgłosić wykonanie?',
      message: 'Zleceniodawca dostanie prośbę o potwierdzenie. Potem możecie się wzajemnie ocenić.',
      confirmText: 'Zgłoś wykonanie',
    },
    success: 'Wykonanie zgłoszone – czekasz na potwierdzenie.',
  },
  complete: {
    question: {
      header: 'Potwierdzić wykonanie?',
      message: 'Zlecenie zostanie zakończone, a rozmowa – zamknięta. Potem możecie się wzajemnie ocenić.',
      confirmText: 'Potwierdź',
    },
    success: 'Zlecenie zakończone. Oceń współpracę!',
  },
};

/**
 * Realizacja przyjętego zlecenia – akcje wspólne dla wątku, szczegółów ogłoszenia i ekranu „Oceny”: krok zlecenia
 * (rozpocznij / zgłoś wykonanie / potwierdź), wyjście (rezygnacja / przerwanie), stanowisko drugiej strony i ocena.
 * Każda metoda: okno albo potwierdzenie → API → komunikat; zwraca `true`, gdy coś się zmieniło (ekran przeładowuje
 * dane). Błędy API pokazuje interceptor – tu kończą się `false`. Negocjacje (oferty) – `OffersService`.
 */
@Injectable({ providedIn: 'root' })
export class JobService {
  private readonly client = inject(AnnouncementsClient);
  private readonly dialogs = inject(DialogService);
  private readonly modals = inject(ModalService);
  private readonly commands = inject(ApiCommands);

  /**
   * Krok z przycisku (`jobStep().action`), po potwierdzeniu: „Rozpocznij” – przyjęty pomocnik (Accepted → InProgress),
   * „Zgłoś wykonanie” – pomocnik (InProgress, raz), „Potwierdź wykonanie” – właściciel (→ Completed, potem oceny).
   */
  async run(announcementId: string, action: JobAction): Promise<boolean> {
    const { question, success } = JOB_ACTIONS[action];
    return (
      (await this.dialogs.confirm(question)) && (await this.commands.run(this.request(announcementId, action), success))
    );
  }

  /**
   * F11 E: „Zrezygnuj” (Accepted → ogłoszenie znów otwarte) albo „Przerwij zlecenie” (InProgress → Interrupted):
   * okno z obowiązkowym powodem → API → komunikat. `otherSide` – „Zleceniodawca” / „Pomocnik” do tekstu w oknie.
   */
  async exit(announcementId: string, action: JobExitAction, otherSide: string): Promise<boolean> {
    const command = await this.modals.open<JobExitCommand>(JobExitModalComponent, { action, otherSide });
    if (!command) {
      return false;
    }
    return action === 'abandon'
      ? this.commands.run(
          this.client.abandon(announcementId, command),
          'Zrezygnowałeś ze zlecenia – druga strona dostała powiadomienie.',
        )
      : this.commands.run(
          this.client.interrupt(announcementId, command),
          'Zlecenie przerwane – druga strona może dodać swoje stanowisko.',
        );
  }

  /**
   * F11 G: stanowisko drugiej strony do rezygnacji / przerwania (raz) – okno z polem tekstowym (2–500 znaków; za krótkie
   * – okno zostaje otwarte z komunikatem) → `POST …/job-exits/{number}/response`.
   */
  async respondToExit(announcementId: string, number: number): Promise<boolean> {
    const text = await this.dialogs.prompt({
      header: 'Twoje stanowisko',
      message: 'Opisz, jak to wyglądało z Twojej strony. Będzie widoczne na profilach obu stron – raz, bez edycji.',
      placeholder: 'Np. zakres prac był inny niż w ogłoszeniu…',
      confirmText: 'Wyślij',
      minLength: OFFER_RULES.message.min,
      maxLength: OFFER_RULES.message.max,
      tooShortMessage: 'Stanowisko: co najmniej 2 znaki.',
    });
    return (
      text !== null &&
      (await this.commands.run(
        this.client.respondToJobExit(announcementId, number, { text }),
        'Stanowisko zapisane – widać je na profilach.',
      ))
    );
  }

  /** Ocena drugiej strony po zakończeniu (raz na ogłoszenie – backend pilnuje, 409 `AlreadyRated`; nie po przerwaniu). */
  async rate(announcementId: string, otherParty: string): Promise<boolean> {
    const rating = await this.modals.open<RateAnnouncement>(RatingModalComponent, { otherParty });
    return (
      !!rating && (await this.commands.run(this.client.postRating(announcementId, rating), 'Dziękujemy za ocenę!'))
    );
  }

  private request(announcementId: string, action: JobAction): Observable<void> {
    switch (action) {
      case 'start':
        return this.client.start(announcementId);
      case 'report':
        return this.client.reportDone(announcementId);
      case 'complete':
        return this.client.complete(announcementId);
    }
  }
}
