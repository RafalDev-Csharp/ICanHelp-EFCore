import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { AnnouncementFieldChangeDto, MakeOffer, OfferDto, ReviseOffer } from '../../core/api/api-client';
import { AnnouncementsClient } from '../../core/api/api-client';
import { ApiCommands } from '../../core/api/api-commands';
import { CategoriesService } from '../../core/categories/categories.service';
import { PushService } from '../../core/pwa/push.service';
import { DialogService } from '../../core/ui/dialog.service';
import { ModalService } from '../../core/ui/modal.service';
import { formatPrice } from '../../shared/pipes/price.pipe';

import { fieldChangeRows } from './offer-changes';
import type { OfferChangesChoice } from './offer-changes-modal.component';
import { OfferChangesModalComponent } from './offer-changes-modal.component';
import { OfferFormModalComponent } from './offer-form-modal.component';
import { changedFieldsSuffix } from './offer-labels';

/**
 * Negocjacje – akcje na ofertach wspólne dla wątku rozmowy i szczegółów ogłoszenia: okno oferty / kontroferty,
 * podgląd zmian ogłoszenia, potwierdzenie nieodwracalnych kroków, wywołanie API, komunikat. Każda metoda zwraca
 * `true`, gdy coś się zmieniło (ekran wtedy przeładowuje dane). Błędy API pokazuje interceptor – tu kończą się
 * `false`. Realizacja przyjętego zlecenia (start, wykonanie, rezygnacja, ocena) – `JobService`.
 */
@Injectable({ providedIn: 'root' })
export class OffersService {
  private readonly client = inject(AnnouncementsClient);
  private readonly dialogs = inject(DialogService);
  private readonly modals = inject(ModalService);
  private readonly commands = inject(ApiCommands);
  private readonly push = inject(PushService);
  private readonly categories = inject(CategoriesService);

  /** Pierwsza oferta pomocnika (cena podpowiedziana z budżetu). Zwraca id oferty albo `null`. */
  async make(announcementId: string, suggestedPrice?: number): Promise<string | null> {
    const command = await this.openForm({ heading: 'Twoja oferta', suggestedPrice });
    if (!command) {
      return null;
    }
    const created = await this.commands.send(this.client.postOffer(announcementId, command), 'Oferta wysłana.');
    if (created) {
      // Dobry moment na pytanie o push: czeka na odpowiedź zleceniodawcy.
      void this.push.suggest('Powiadomić Cię, gdy zleceniodawca odpowie na ofertę?');
    }
    return created?.result?.id ?? null;
  }

  /** Kontroferta drugiej strony; dla nieaktualnej oferty – z informacją, co zmieniło się w ogłoszeniu. */
  async counter(offer: OfferDto): Promise<boolean> {
    const changed = changedFieldsSuffix(offer.changesSinceOffer);
    const command = await this.openForm({
      heading: 'Kontroferta',
      basis: offer,
      note: offer.isOutdated
        ? `Ogłoszenie zmieniło się od tej oferty${changed}. Zaproponuj warunki do aktualnej wersji.`
        : undefined,
    });
    return (
      !!command &&
      (await this.commands.run(
        this.client.postCounterOffer(offer.announcementId, offer.id, command),
        'Kontroferta wysłana.',
      ))
    );
  }

  /**
   * Autor zmienia własną oczekującą ofertę, zanim druga strona odpowie (F11 D – także aktualną): okno z poprzednimi
   * warunkami → `POST …/revise` (stara oferta → „Zaktualizowana”). Nieaktualna – z informacją, co się zmieniło.
   */
  async revise(offer: OfferDto): Promise<boolean> {
    const changed = changedFieldsSuffix(offer.changesSinceOffer);
    const command = await this.openForm({
      heading: offer.isOutdated ? 'Zaktualizuj ofertę' : 'Zmień ofertę',
      basis: offer,
      note: offer.isOutdated
        ? `Ogłoszenie zmieniło się od Twojej oferty${changed}. Sprawdź warunki i wyślij je do aktualnej wersji – dopiero wtedy druga strona może ją przyjąć.`
        : 'Nowe warunki zastąpią poprzednie – druga strona dostanie powiadomienie.',
    });
    return (
      !!command &&
      (await this.commands.run(
        this.client.postReviseOffer(offer.announcementId, offer.id, command),
        offer.isOutdated ? 'Oferta zaktualizowana.' : 'Oferta zmieniona.',
      ))
    );
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
  keep(offer: OfferDto): Promise<boolean> {
    const command: ReviseOffer = {
      price: offer.price,
      proposedStart: offer.proposedStart,
      proposedEnd: offer.proposedEnd,
      message: offer.message,
    };
    return this.commands.run(
      this.client.postReviseOffer(offer.announcementId, offer.id, command),
      'Oferta podtrzymana – druga strona może ją teraz przyjąć.',
    );
  }

  /** Sam podgląd jednej edycji ogłoszenia (wpis „Ogłoszenie zmienione” w wątku). */
  async showChanges(changes: readonly AnnouncementFieldChangeDto[], intro = ''): Promise<void> {
    await this.openChanges(changes, { heading: 'Zmiany w ogłoszeniu', intro, withActions: false });
  }

  async accept(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.dialogs.confirm({
      header: 'Przyjąć ofertę?',
      message: `${formatPrice(offer.price.amount)} – pozostałe oferty zostaną odrzucone, a Wy zobaczycie swoje dane kontaktowe.`,
      confirmText: 'Przyjmij',
    });
    return (
      confirmed && (await this.commands.run(this.client.accept(offer.announcementId, offer.id), 'Oferta przyjęta.'))
    );
  }

  async reject(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.dialogs.confirm({
      header: 'Odrzucić ofertę?',
      message: 'Druga strona może potem złożyć nową.',
      confirmText: 'Odrzuć',
      destructive: true,
    });
    return (
      confirmed && (await this.commands.run(this.client.reject(offer.announcementId, offer.id), 'Oferta odrzucona.'))
    );
  }

  async withdraw(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.dialogs.confirm({
      header: 'Wycofać ofertę?',
      message: 'Możesz potem złożyć nową.',
      confirmText: 'Wycofaj',
      destructive: true,
    });
    return (
      confirmed && (await this.commands.run(this.client.withdraw(offer.announcementId, offer.id), 'Oferta wycofana.'))
    );
  }

  private async openChanges(
    changes: readonly AnnouncementFieldChangeDto[],
    props: Pick<OfferChangesModalComponent, 'heading' | 'intro' | 'withActions'>,
  ): Promise<OfferChangesChoice | null> {
    // Nazwy kategorii do wiersza „Kategoria” – zwykle już wczytane (lista, formularz); błąd = same id nie psują okna.
    await firstValueFrom(this.categories.load(), { defaultValue: [] }).catch(() => []);
    const rows = fieldChangeRows(changes, (id) => this.categories.name(id) || 'inna');
    return this.modals.open<OfferChangesChoice>(OfferChangesModalComponent, { ...props, rows });
  }

  private openForm(props: Partial<OfferFormModalComponent>): Promise<MakeOffer | null> {
    return this.modals.open<MakeOffer>(OfferFormModalComponent, props);
  }
}
