import type { OfferDto } from '../../core/api/api-client';

import type { OfferActions } from './offer-labels';
import {
  changedFieldsLabel,
  changedFieldsSuffix,
  offerActions,
  offerKindLabel,
  offerStateColor,
  offerStateLabel,
  partyGenitive,
  partyLabel,
} from './offer-labels';

/**
 * Akcja na pojedynczej ofercie – nazwa metody `OffersService` (każda: okno / potwierdzenie → API → komunikat).
 * `reviewChanges` – podgląd zmian ogłoszenia i decyzja autora oferty sprzed edycji.
 */
export type OfferAction = 'accept' | 'reject' | 'counter' | 'withdraw' | 'revise' | 'reviewChanges';

/** Przycisk akcji pod ofertą. */
export interface OfferButton {
  action: OfferAction;
  label: string;
  /** Bez wartości – domyślny (pełny) przycisk Ionic. */
  fill?: 'outline' | 'solid';
  color?: string;
  icon?: string;
}

/** Oferta gotowa do narysowania w wątku (`app-offer-card`) – teksty i dozwolone akcje wyliczone dla czytającego. */
export interface OfferView {
  offer: OfferDto;
  /** Autorem jest czytający. */
  mine: boolean;
  /** „Ty” albo rola autora („Pomocnik” / „Zleceniodawca”). */
  author: string;
  /** „Oferta” / „Kontroferta” / „Zaktualizowana oferta” (`offerKindLabel`). */
  kindLabel: string;
  /** Etykieta statusu bez „nieaktualna” (`offerStateLabel`) i jej kolor. */
  stateLabel: string;
  stateColor: string;
  actions: OfferActions;
  /** „budżet, opis” – co zmieniło się w ogłoszeniu od złożenia oferty (tylko oczekujące sprzed edycji). */
  changedSince: string;
  /**
   * Oczekująca oferta sprzed edycji ogłoszenia (F11 F): `respond` – jestem jej autorem, mam ją podtrzymać, zmienić
   * albo wycofać (najpierw podgląd zmian); `wait` – druga strona ma odpowiedzieć na zmiany (spokojna notka).
   */
  changes: 'respond' | 'wait' | null;
  /** Zdanie pod ofertą sprzed edycji ogłoszenia – co się stało i kto ma teraz ruch (pusty bez `changes`). */
  changesNote: string;
  /** Przyciski w kolejności wyświetlania (pusta lista = nic do zrobienia). */
  buttons: OfferButton[];
  /** Poprzednia wersja oferty zastąpiona przez autora (`Revised`) – zwinięta. */
  previous: boolean;
}

/**
 * @param offers oferty tej samej negocjacji (do rozpoznania „Zaktualizowana oferta”)
 * @param myRole rola czytającego w wątku (`Client` / `Helper`)
 * @param announcementStatus oferty żyją tylko w ogłoszeniu `Open`
 */
export function offerView(
  offer: OfferDto,
  offers: readonly OfferDto[],
  myRole: string,
  announcementStatus: string,
): OfferView {
  const mine = offer.proposedBy === myRole;
  const outdated = offer.isOutdated && offer.status === 'Pending' && announcementStatus === 'Open';
  const changes = outdated ? (mine ? 'respond' : 'wait') : null;
  const actions = offerActions(offer, myRole, announcementStatus);
  return {
    offer,
    mine,
    author: mine ? 'Ty' : partyLabel(offer.proposedBy),
    kindLabel: offerKindLabel(offer, offers),
    stateLabel: offerStateLabel(offer),
    stateColor: offerStateColor(offer),
    actions,
    changedSince: outdated ? changedFieldsLabel(offer.changesSinceOffer) : '',
    changes,
    changesNote: changesNote(changes, offer, myRole),
    buttons: offerButtons(actions, changes),
    previous: offer.status === 'Revised',
  };
}

/**
 * Przyciski pod ofertą. Autor oferty sprzed edycji ogłoszenia ma jeden – „Zobacz zmiany” (decyzję podejmuje w oknie
 * zmian); poza tym: autor – „Wycofaj” / „Zmień ofertę”, druga strona – „Odrzuć” / kontroferta / „Przyjmij”.
 * Bez „Przyjmij” (oferta sprzed edycji) kontroferta jest główną akcją.
 */
export function offerButtons(actions: OfferActions, changes: OfferView['changes']): OfferButton[] {
  if (changes === 'respond') {
    return [{ action: 'reviewChanges', label: 'Zobacz zmiany', icon: 'eye-outline' }];
  }
  const buttons: OfferButton[] = [];
  if (actions.withdraw) {
    buttons.push({ action: 'withdraw', label: 'Wycofaj', fill: 'outline', color: 'medium' });
  }
  if (actions.revise) {
    buttons.push({ action: 'revise', label: 'Zmień ofertę', fill: 'outline' });
  }
  if (actions.reject) {
    buttons.push({ action: 'reject', label: 'Odrzuć', fill: 'outline', color: 'danger' });
  }
  if (actions.counter) {
    buttons.push(
      actions.accept
        ? { action: 'counter', label: 'Kontroferta', fill: 'outline' }
        : { action: 'counter', label: 'Zaproponuj kontrofertę', fill: 'solid' },
    );
  }
  if (actions.accept) {
    buttons.push({ action: 'accept', label: 'Przyjmij', color: 'success' });
  }
  return buttons;
}

function changesNote(changes: OfferView['changes'], offer: OfferDto, myRole: string): string {
  const changed = changedFieldsSuffix(offer.changesSinceOffer);
  switch (changes) {
    case 'respond': {
      const who = myRole === 'Helper' ? 'Zleceniodawca zmienił ogłoszenie' : 'Zmieniłeś ogłoszenie';
      return `${who} po tej ofercie${changed}. Zobacz zmiany i podtrzymaj ofertę, zmień ją albo wycofaj.`;
    }
    case 'wait':
      return `Czeka na odpowiedź ${partyGenitive(offer.proposedBy)} na zmiany w ogłoszeniu${changed}.`;
    default:
      return '';
  }
}
