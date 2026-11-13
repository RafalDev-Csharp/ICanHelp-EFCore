import { Injectable, inject } from '@angular/core';
import { AlertController, ModalController } from '@ionic/angular';
import type { Observable } from 'rxjs';
import { firstValueFrom } from 'rxjs';

import type {
  AnnouncementFieldChangeDto,
  MakeOffer,
  OfferDto,
  RateAnnouncement,
  ReviseOffer,
} from '../../core/api/api-client';
import { AnnouncementsClient } from '../../core/api/api-client';
import { CategoriesService } from '../../core/categories/categories.service';
import { PushService } from '../../core/pwa/push.service';
import { ToastService } from '../../core/ui/toast.service';
import { formatPrice } from '../../shared/pipes/price.pipe';

import type { JobExitAction, JobExitCommand } from './job-exit';
import { JobExitModalComponent } from './job-exit-modal.component';
import { fieldChangeRows } from './offer-changes';
import type { OfferChangesChoice } from './offer-changes-modal.component';
import { OfferChangesModalComponent } from './offer-changes-modal.component';
import { OfferFormModalComponent } from './offer-form-modal.component';
import { changedFieldsLabel } from './offer-labels';
import { RatingModalComponent } from './rating-modal.component';

/**
 * Akcje negocjacji wspólne dla wątku rozmowy i szczegółów ogłoszenia: okno oferty / kontroferty / oceny,
 * potwierdzenie nieodwracalnych kroków, wywołanie API, komunikat. Każda metoda zwraca `true`, gdy coś się zmieniło
 * (ekran wtedy przeładowuje dane). Błędy API pokazuje interceptor – tu kończą się `false`.
 */
@Injectable({ providedIn: 'root' })
export class OffersService {
  private readonly client = inject(AnnouncementsClient);
  private readonly modals = inject(ModalController);
  private readonly alerts = inject(AlertController);
  private readonly toast = inject(ToastService);
  private readonly push = inject(PushService);
  private readonly categories = inject(CategoriesService);

  /** Pierwsza oferta pomocnika (cena podpowiedziana z budżetu). Zwraca id oferty albo `null`. */
  async make(announcementId: string, suggestedPrice?: number): Promise<string | null> {
    const command = await this.openForm({ heading: 'Twoja oferta', suggestedPrice });
    if (!command) {
      return null;
    }
    const created = await this.run(this.client.postOffer(announcementId, command), 'Oferta wysłana.');
    if (created) {
      // Dobry moment na pytanie o push: czeka na odpowiedź zleceniodawcy.
      void this.push.suggest('Powiadomić Cię, gdy zleceniodawca odpowie na ofertę?');
    }
    return created?.result?.id ?? null;
  }

  /** Kontroferta drugiej strony; dla nieaktualnej oferty – z informacją, co zmieniło się w ogłoszeniu. */
  async counter(offer: OfferDto): Promise<boolean> {
    const changed = changedFieldsLabel(offer.changesSinceOffer);
    const command = await this.openForm({
      heading: 'Kontroferta',
      basis: offer,
      note: offer.isOutdated
        ? `Ogłoszenie zmieniło się od tej oferty${changed ? ` (${changed})` : ''}. Zaproponuj warunki do aktualnej wersji.`
        : undefined,
    });
    if (!command) {
      return false;
    }
    const created = await this.run(
      this.client.postCounterOffer(offer.announcementId, offer.id, command),
      'Kontroferta wysłana.',
    );
    return created !== null;
  }

  /**
   * Autor zmienia własną oczekującą ofertę, zanim druga strona odpowie (F11 D – także aktualną): okno z poprzednimi
   * warunkami → `POST …/revise` (stara oferta → „Zaktualizowana”). Nieaktualna – z informacją, co się zmieniło.
   */
  async revise(offer: OfferDto): Promise<boolean> {
    const changed = changedFieldsLabel(offer.changesSinceOffer);
    const command = await this.openForm({
      heading: offer.isOutdated ? 'Zaktualizuj ofertę' : 'Zmień ofertę',
      basis: offer,
      note: offer.isOutdated
        ? `Ogłoszenie zmieniło się od Twojej oferty${changed ? ` (${changed})` : ''}. Sprawdź warunki i wyślij je do aktualnej wersji – dopiero wtedy druga strona może ją przyjąć.`
        : 'Nowe warunki zastąpią poprzednie – druga strona dostanie powiadomienie.',
    });
    if (!command) {
      return false;
    }
    const created = await this.run(
      this.client.postReviseOffer(offer.announcementId, offer.id, command),
      offer.isOutdated ? 'Oferta zaktualizowana.' : 'Oferta zmieniona.',
    );
    return created !== null;
  }

  /**
   * Autor oferty sprzed edycji ogłoszenia (F11 F): najpierw **podgląd zmian** (było → jest), dopiero z niego decyzja –
   * „Podtrzymuję ofertę” (`keep`), „Zmień ofertę” (formularz – `revise`) albo „Wycofaj” (`withdraw`).
   */
  async reviewChanges(offer: OfferDto): Promise<boolean> {
    const helper = offer.proposedBy === 'Helper';
    const choice = await this.openChanges(offer.changesSinceOffer, {
      heading: 'Zmiany w ogłoszeniu',
      intro: helper
        ? 'Zleceniodawca zmienił ogłoszenie po Twojej ofercie. Podtrzymaj ją, zmień albo wycofaj – dopiero wtedy może ją przyjąć.'
        : 'Zmieniłeś ogłoszenie po swojej kontrofercie. Podtrzymaj ją, zmień albo wycofaj – dopiero wtedy pomocnik może ją przyjąć.',
      withActions: true,
    });
    switch (choice) {
      case 'keep':
        return this.keep(offer);
      case 'change':
        return this.revise(offer);
      case 'withdraw':
        return this.withdraw(offer);
      default:
        return false;
    }
  }

  /**
   * „Podtrzymuję ofertę” – jednym kliknięciem, bez formularza: te same warunki do aktualnej wersji ogłoszenia
   * (`POST …/revise` z niezmienioną ceną, terminem i uzasadnieniem; stara oferta → „Zaktualizowana”).
   */
  async keep(offer: OfferDto): Promise<boolean> {
    const command: ReviseOffer = {
      price: offer.price,
      proposedStart: offer.proposedStart,
      proposedEnd: offer.proposedEnd,
      message: offer.message,
    };
    const created = await this.run(
      this.client.postReviseOffer(offer.announcementId, offer.id, command),
      'Oferta podtrzymana – druga strona może ją teraz przyjąć.',
    );
    return created !== null;
  }

  /** Sam podgląd jednej edycji ogłoszenia (wpis „Ogłoszenie zmienione” w wątku). */
  async showChanges(changes: readonly AnnouncementFieldChangeDto[], intro = ''): Promise<void> {
    await this.openChanges(changes, { heading: 'Zmiany w ogłoszeniu', intro, withActions: false });
  }

  async accept(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.confirm(
      'Przyjąć ofertę?',
      `${formatPrice(offer.price.amount)} – pozostałe oferty zostaną odrzucone, a Wy zobaczycie swoje dane kontaktowe.`,
      'Przyjmij',
    );
    return (
      confirmed && (await this.run(this.client.accept(offer.announcementId, offer.id), 'Oferta przyjęta.')) !== null
    );
  }

  async reject(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.confirm('Odrzucić ofertę?', 'Druga strona może potem złożyć nową.', 'Odrzuć', true);
    return (
      confirmed && (await this.run(this.client.reject(offer.announcementId, offer.id), 'Oferta odrzucona.')) !== null
    );
  }

  async withdraw(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.confirm('Wycofać ofertę?', 'Możesz potem złożyć nową.', 'Wycofaj', true);
    return (
      confirmed && (await this.run(this.client.withdraw(offer.announcementId, offer.id), 'Oferta wycofana.')) !== null
    );
  }

  /** „Rozpocznij” – tylko przyjęty pomocnik (Accepted → InProgress); zleceniodawca dostaje powiadomienie. */
  async start(announcementId: string): Promise<boolean> {
    const confirmed = await this.confirm(
      'Rozpocząć zlecenie?',
      'Zleceniodawca dostanie informację, że zaczynasz. Po wszystkim zgłosisz wykonanie.',
      'Rozpocznij',
    );
    return confirmed && (await this.run(this.client.start(announcementId), 'Zlecenie w realizacji.')) !== null;
  }

  /** „Zgłoś wykonanie” – przyjęty pomocnik (InProgress, raz); zleceniodawca potwierdza. */
  async reportDone(announcementId: string): Promise<boolean> {
    const confirmed = await this.confirm(
      'Zgłosić wykonanie?',
      'Zleceniodawca dostanie prośbę o potwierdzenie. Potem możecie się wzajemnie ocenić.',
      'Zgłoś wykonanie',
    );
    return (
      confirmed &&
      (await this.run(this.client.reportDone(announcementId), 'Wykonanie zgłoszone – czekasz na potwierdzenie.')) !==
        null
    );
  }

  /** „Potwierdź wykonanie” – tylko właściciel (Accepted / InProgress → Completed), potem obie strony mogą się ocenić. */
  async complete(announcementId: string): Promise<boolean> {
    const confirmed = await this.confirm(
      'Potwierdzić wykonanie?',
      'Zlecenie zostanie zakończone, a rozmowa – zamknięta. Potem możecie się wzajemnie ocenić.',
      'Potwierdź',
    );
    return (
      confirmed &&
      (await this.run(this.client.complete(announcementId), 'Zlecenie zakończone. Oceń współpracę!')) !== null
    );
  }

  /**
   * F11 E: „Zrezygnuj” (Accepted → ogłoszenie znów otwarte) albo „Przerwij zlecenie” (InProgress → Interrupted):
   * okno z obowiązkowym powodem → API → toast. `otherSide` – „Zleceniodawca” / „Pomocnik” do tekstu w oknie.
   */
  async exitJob(announcementId: string, action: JobExitAction, otherSide: string): Promise<boolean> {
    const modal = await this.modals.create({ component: JobExitModalComponent, componentProps: { action, otherSide } });
    await modal.present();
    const { data, role } = await modal.onWillDismiss<JobExitCommand>();
    if (role !== 'confirm' || !data) {
      return false;
    }
    const request =
      action === 'abandon' ? this.client.abandon(announcementId, data) : this.client.interrupt(announcementId, data);
    const success =
      action === 'abandon'
        ? 'Zrezygnowałeś ze zlecenia – druga strona dostała powiadomienie.'
        : 'Zlecenie przerwane – druga strona może dodać swoje stanowisko.';
    return (await this.run(request, success)) !== null;
  }

  /**
   * F11 G: stanowisko drugiej strony do rezygnacji / przerwania (raz) – okno z polem tekstowym (2–500 znaków; za krótkie
   * – okno zostaje otwarte z komunikatem) → `POST …/job-exits/{number}/response`.
   */
  async respondToExit(announcementId: string, number: number): Promise<boolean> {
    const alert = await this.alerts.create({
      header: 'Twoje stanowisko',
      message: 'Opisz, jak to wyglądało z Twojej strony. Będzie widoczne na profilach obu stron – raz, bez edycji.',
      inputs: [
        {
          name: 'text',
          type: 'textarea',
          placeholder: 'Np. zakres prac był inny niż w ogłoszeniu…',
          attributes: { maxlength: 500 },
        },
      ],
      buttons: [
        { text: 'Anuluj', role: 'cancel' },
        {
          text: 'Wyślij',
          role: 'confirm',
          handler: (values: { text?: string }) => {
            if ((values.text ?? '').trim().length < 2) {
              void this.toast.error('Stanowisko: co najmniej 2 znaki.');
              return false;
            }
            return true;
          },
        },
      ],
    });
    await alert.present();
    const { data, role } = await alert.onDidDismiss<{ values?: { text?: string } }>();
    const text = data?.values?.text?.trim() ?? '';
    if (role !== 'confirm' || text.length < 2) {
      return false;
    }
    return (
      (await this.run(
        this.client.respondToJobExit(announcementId, number, { text }),
        'Stanowisko zapisane – widać je na profilach.',
      )) !== null
    );
  }

  /** Ocena drugiej strony po zakończeniu (raz na ogłoszenie – backend pilnuje, 409 `AlreadyRated`; nie po przerwaniu). */
  async rate(announcementId: string, otherParty: string): Promise<boolean> {
    const modal = await this.modals.create({ component: RatingModalComponent, componentProps: { otherParty } });
    await modal.present();
    const { data, role } = await modal.onWillDismiss<RateAnnouncement>();
    if (role !== 'confirm' || !data) {
      return false;
    }
    return (await this.run(this.client.postRating(announcementId, data), 'Dziękujemy za ocenę!')) !== null;
  }

  private async openChanges(
    changes: readonly AnnouncementFieldChangeDto[],
    props: Pick<OfferChangesModalComponent, 'heading' | 'intro' | 'withActions'>,
  ): Promise<OfferChangesChoice | null> {
    // Nazwy kategorii do wiersza „Kategoria” – zwykle już wczytane (lista, formularz); błąd = same id nie psują okna.
    await firstValueFrom(this.categories.load(), { defaultValue: [] }).catch(() => []);
    const rows = fieldChangeRows(changes, (id) => this.categories.name(id) || 'inna');
    const modal = await this.modals.create({
      component: OfferChangesModalComponent,
      componentProps: { ...props, rows },
    });
    await modal.present();
    const { data, role } = await modal.onWillDismiss<OfferChangesChoice>();
    return role === 'confirm' && data ? data : null;
  }

  private async openForm(props: Partial<OfferFormModalComponent>): Promise<MakeOffer | null> {
    const modal = await this.modals.create({ component: OfferFormModalComponent, componentProps: props });
    await modal.present();
    const { data, role } = await modal.onWillDismiss<MakeOffer>();
    return role === 'confirm' && data ? data : null;
  }

  private async confirm(header: string, message: string, action: string, destructive = false): Promise<boolean> {
    const alert = await this.alerts.create({
      header,
      message,
      buttons: [
        { text: 'Nie', role: 'cancel' },
        { text: action, role: destructive ? 'destructive' : 'confirm' },
      ],
    });
    await alert.present();
    const { role } = await alert.onDidDismiss();
    return role === 'confirm' || role === 'destructive';
  }

  /** `{ result }` po sukcesie (także dla `void` z API), `null` przy błędzie (komunikat pokazał interceptor). */
  private async run<T>(request: Observable<T>, success: string): Promise<{ result: T | undefined } | null> {
    try {
      const result = await firstValueFrom(request, { defaultValue: undefined });
      void this.toast.success(success);
      return { result };
    } catch {
      return null;
    }
  }
}
