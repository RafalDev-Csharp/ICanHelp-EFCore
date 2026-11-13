import type { AnnouncementFieldChangeDto, ConversationDto, MessageDto, OfferDto } from '../../core/api/api-client';
import type { OfferActions } from '../offers/offer-labels';
import {
  changedFieldsLabel,
  offerActions,
  offerKindLabel,
  offerStateColor,
  offerStateLabel,
  partyLabel,
  pendingOffer,
} from '../offers/offer-labels';

/** Wpis osi czasu wątku gotowy do wyświetlenia (typy wpisów jak `ConversationEntryDto.type` w API). */
export type TimelineItem =
  | { kind: 'message'; key: string; at: string; message: MessageDto; mine: boolean }
  | {
      kind: 'offer';
      key: string;
      at: string;
      offer: OfferDto;
      mine: boolean;
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
      /** Poprzednia wersja oferty zastąpiona przez autora (`Revised`) – zwinięta. */
      previous: boolean;
    }
  | {
      kind: 'change';
      key: string;
      at: string;
      fields: string;
      revision: number;
      /** Pola „było → jest” tej edycji (podgląd zmian). */
      changes: AnnouncementFieldChangeDto[];
      /** Edytował czytający (zleceniodawca) – „Edytowałeś ogłoszenie”, inaczej „Zleceniodawca zmienił ogłoszenie”. */
      mine: boolean;
    };

/**
 * Oś czasu od najstarszych (kolejność z API). Wpisy z nieznanym `type` pomijamy – nowsza wersja backendu
 * może dodać inne rodzaje, stara aplikacja ich po prostu nie pokaże.
 */
export function toTimelineItems(conversation: ConversationDto, myId: string | undefined): TimelineItem[] {
  const items: TimelineItem[] = [];
  const offers = offersOf(conversation);
  for (const entry of conversation.timeline) {
    if (entry.type === 'Message' && entry.message) {
      items.push({
        kind: 'message',
        key: `m-${entry.message.id}`,
        at: entry.at,
        message: entry.message,
        mine: entry.message.senderId === myId,
      });
    } else if (entry.type === 'Offer' && entry.offer) {
      const offer = entry.offer;
      const mine = offer.proposedBy === conversation.myRole;
      const outdated = offer.isOutdated && offer.status === 'Pending' && conversation.announcementStatus === 'Open';
      items.push({
        kind: 'offer',
        key: `o-${offer.id}`,
        at: entry.at,
        offer,
        mine,
        kindLabel: offerKindLabel(offer, offers),
        stateLabel: offerStateLabel(offer),
        stateColor: offerStateColor(offer),
        actions: offerActions(offer, conversation.myRole, conversation.announcementStatus),
        changedSince: outdated ? changedFieldsLabel(offer.changesSinceOffer) : '',
        changes: outdated ? (mine ? 'respond' : 'wait') : null,
        previous: offer.status === 'Revised',
      });
    } else if (entry.type === 'AnnouncementChanged' && entry.change) {
      items.push({
        kind: 'change',
        key: `c-${entry.change.revision}`,
        at: entry.at,
        fields: changedFieldsLabel(entry.change.fields),
        revision: entry.change.revision,
        changes: entry.change.fields,
        mine: conversation.myRole === 'Client',
      });
    }
  }
  return items;
}

/** Oferty wątku (do „czy mam oczekującą ofertę”, podpowiedzi ceny). */
export function offersOf(conversation: ConversationDto): OfferDto[] {
  return conversation.timeline.filter((entry) => entry.type === 'Offer' && entry.offer).map((entry) => entry.offer);
}

/** Wyróżniona informacja nad polem wiadomości: co czeka na czytającego (F11 F). */
export interface ThreadNotice {
  /** `changes` – podgląd zmian ogłoszenia i decyzja o ofercie; `revised` – przewiń do aktualnej oferty. */
  kind: 'changes' | 'revised';
  text: string;
  offer: OfferDto;
}

/**
 * Jedna informacja dla czytającego na podstawie oczekującej oferty (najwyżej jedna w wątku):
 * - jestem autorem, a ogłoszenie zmieniono po ofercie → „Zleceniodawca zmienił ogłoszenie – zobacz zmiany”
 *   (u właściciela z własną kontrofertą – „Zmieniłeś ogłoszenie po swojej kontrofercie – zobacz zmiany”);
 * - druga strona zmieniła swoją ofertę (poprzednia `Revised`) → „Pomocnik zmienił ofertę – zobacz aktualną”.
 */
export function threadNotice(conversation: ConversationDto): ThreadNotice | null {
  if (conversation.announcementStatus !== 'Open') {
    return null;
  }
  const offers = offersOf(conversation);
  const offer = pendingOffer(offers);
  if (!offer) {
    return null;
  }
  const mine = offer.proposedBy === conversation.myRole;
  if (mine && offer.isOutdated) {
    return {
      kind: 'changes',
      text:
        conversation.myRole === 'Helper'
          ? 'Zleceniodawca zmienił ogłoszenie – zobacz zmiany'
          : 'Zmieniłeś ogłoszenie po swojej kontrofercie – zobacz zmiany',
      offer,
    };
  }
  const previous = offers.find((other) => other.id === offer.parentOfferId);
  if (!mine && !offer.isOutdated && previous?.status === 'Revised') {
    const party = partyLabel(offer.proposedBy);
    // „Podtrzymuję ofertę” = te same warunki do nowej wersji ogłoszenia – nie „zmienił”, tylko „podtrzymał”.
    const text = sameTerms(offer, previous)
      ? `${party} podtrzymał ofertę po zmianach – zobacz aktualną`
      : `${party} zmienił ofertę – zobacz aktualną`;
    return { kind: 'revised', text, offer };
  }
  return null;
}

function sameTerms(a: OfferDto, b: OfferDto): boolean {
  return (
    a.price.amount === b.price.amount &&
    a.proposedStart === b.proposedStart &&
    a.proposedEnd === b.proposedEnd &&
    (a.message ?? '') === (b.message ?? '')
  );
}
