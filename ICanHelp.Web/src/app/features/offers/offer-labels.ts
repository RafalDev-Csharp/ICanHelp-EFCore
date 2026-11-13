import type { AnnouncementFieldChangeDto, OfferDto } from '../../core/api/api-client';
import { APP_LOCALE } from '../../core/config/locale';
import { parseApiDate } from '../../shared/pipes/time-ago.pipe';

const dayFormat = new Intl.DateTimeFormat(APP_LOCALE, { weekday: 'short', day: 'numeric', month: 'short' });
const timeFormat = new Intl.DateTimeFormat(APP_LOCALE, { hour: '2-digit', minute: '2-digit' });

/** Termin oferty: „wt., 10 lis, 17:00–19:00”, a przez kilka dni „wt., 10 lis, 17:00 – śr., 11 lis, 12:00”. */
export function formatOfferTerm(start: string, end: string): string {
  const from = parseApiDate(start);
  const to = parseApiDate(end);
  const fromText = `${dayFormat.format(from)}, ${timeFormat.format(from)}`;
  return from.toDateString() === to.toDateString()
    ? `${fromText}–${timeFormat.format(to)}`
    : `${fromText} – ${dayFormat.format(to)}, ${timeFormat.format(to)}`;
}

/** Data i godzina wpisu: „czw., 13 lis, 14:05” (np. „Edytowałeś ogłoszenie · …”). */
export function formatDateTime(value: string): string {
  const date = parseApiDate(value);
  return `${dayFormat.format(date)}, ${timeFormat.format(date)}`;
}

/** Teksty enumów ofert z API (`OfferStatus`, `OfferParty`, `AnnouncementFields`) – nieznana wartość zostaje bez tłumaczenia. */
const OFFER_STATUS_LABELS: Readonly<Record<string, string>> = {
  Pending: 'Czeka na odpowiedź',
  Accepted: 'Przyjęta',
  Rejected: 'Odrzucona',
  Withdrawn: 'Wycofana',
  Countered: 'Kontroferta',
  Revised: 'Zaktualizowana',
};

const OFFER_STATUS_COLORS: Readonly<Record<string, string>> = {
  Pending: 'warning',
  Accepted: 'success',
  Rejected: 'danger',
  Withdrawn: 'medium',
  Countered: 'medium',
  Revised: 'medium',
};

const PARTY_LABELS: Readonly<Record<string, string>> = {
  Client: 'Zleceniodawca',
  Helper: 'Pomocnik',
};

const FIELD_LABELS: Readonly<Record<string, string>> = {
  Category: 'kategoria',
  Difficulty: 'trudność',
  Title: 'tytuł',
  Description: 'opis',
  Budget: 'budżet',
  Location: 'miejsce',
  Images: 'zdjęcia',
};

export function offerStatusLabel(status: string): string {
  return OFFER_STATUS_LABELS[status] ?? status;
}

/** Kolor Ionic (`ion-badge [color]`) dla statusu oferty. */
export function offerStatusColor(status: string): string {
  return OFFER_STATUS_COLORS[status] ?? 'medium';
}

/**
 * Etykieta oferty na listach i w wątku (F11 F): oczekująca oferta sprzed edycji ogłoszenia to nie „nieaktualna”,
 * tylko „Czeka na odpowiedź na zmiany” – autor ma ją podtrzymać, zmienić albo wycofać. Inaczej – `offerStatusLabel`.
 */
export function offerStateLabel(offer: Pick<OfferDto, 'status' | 'isOutdated'>): string {
  return offer.status === 'Pending' && offer.isOutdated
    ? 'Czeka na odpowiedź na zmiany'
    : offerStatusLabel(offer.status);
}

/** Kolor do `offerStateLabel` – oferta czekająca na odpowiedź na zmiany jest spokojna (szara), nie ostrzeżeniem. */
export function offerStateColor(offer: Pick<OfferDto, 'status' | 'isOutdated'>): string {
  return offer.status === 'Pending' && offer.isOutdated ? 'medium' : offerStatusColor(offer.status);
}

/** „Zleceniodawca” / „Pomocnik” (`OfferParty`, `myRole`, `senderRole`). */
export function partyLabel(party: string): string {
  return PARTY_LABELS[party] ?? party;
}

/** „budżet, opis” – co zmieniło się w ogłoszeniu (pola rewizji albo `changesSinceOffer`). */
export function changedFieldsLabel(fields: readonly AnnouncementFieldChangeDto[]): string {
  return [...new Set(fields.map((change) => FIELD_LABELS[change.field] ?? change.field))].join(', ');
}

/**
 * „Oferta” (pierwsza), „Kontroferta” (odpowiedź drugiej strony) albo „Zaktualizowana oferta” (autor zastąpił swoją
 * nieaktualną ofertę – poprzednia ma status `Revised`). `offers` – oferty tej samej negocjacji (do znalezienia poprzedniej).
 */
export function offerKindLabel(
  offer: Pick<OfferDto, 'parentOfferId'>,
  offers: readonly Pick<OfferDto, 'id' | 'status'>[],
): string {
  if (!offer.parentOfferId) {
    return 'Oferta';
  }
  const parent = offers.find((other) => other.id === offer.parentOfferId);
  return parent?.status === 'Revised' ? 'Zaktualizowana oferta' : 'Kontroferta';
}

/** Co zalogowany może zrobić z ofertą – te same reguły co w domenie (`Announcement.AcceptOffer` itd.). */
export interface OfferActions {
  /** Akceptacja: druga strona, oferta aktualna (złożona na ostatniej rewizji ogłoszenia). */
  accept: boolean;
  reject: boolean;
  /** Kontroferta: druga strona; dla nieaktualnej oferty to jedyna droga do porozumienia. */
  counter: boolean;
  /** Wycofanie: tylko autor. */
  withdraw: boolean;
  /**
   * Zmiana: autor oczekującej oferty (`Announcement.ReviseOffer`, F11 D – także aktualnej) – nowe warunki do bieżącej
   * wersji ogłoszenia, dopóki druga strona nie odpowie.
   */
  revise: boolean;
}

const NO_ACTIONS: OfferActions = { accept: false, reject: false, counter: false, withdraw: false, revise: false };

/**
 * @param myParty rola zalogowanego w tej negocjacji (`Client` = właściciel, `Helper` = pomocnik z oferty)
 * @param announcementStatus oferty żyją tylko w ogłoszeniu `Open`
 */
export function offerActions(
  offer: Pick<OfferDto, 'status' | 'proposedBy' | 'isOutdated'>,
  myParty: string,
  announcementStatus: string,
): OfferActions {
  if (offer.status !== 'Pending' || announcementStatus !== 'Open') {
    return NO_ACTIONS;
  }
  if (offer.proposedBy === myParty) {
    return { ...NO_ACTIONS, withdraw: true, revise: true };
  }
  return { ...NO_ACTIONS, accept: !offer.isOutdated, reject: true, counter: true };
}

/** Oferta oczekująca w łańcuchu negocjacji jednego pomocnika (najwyżej jedna), albo `undefined`. */
export function pendingOffer(offers: readonly OfferDto[]): OfferDto | undefined {
  return offers.find((offer) => offer.status === 'Pending');
}
