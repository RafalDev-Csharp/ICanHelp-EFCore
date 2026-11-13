import type { ConversationDto, MessageDto, OfferDto } from '../../core/api/api-client';
import type { OfferActions } from '../offers/offer-labels';
import { changedFieldsLabel, offerActions, offerKindLabel } from '../offers/offer-labels';

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
      actions: OfferActions;
      /** „budżet, opis” – co zmieniło się w ogłoszeniu od złożenia oferty (tylko dla nieaktualnych). */
      changedSince: string;
    }
  | { kind: 'change'; key: string; at: string; fields: string; revision: number };

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
      items.push({
        kind: 'offer',
        key: `o-${offer.id}`,
        at: entry.at,
        offer,
        mine: offer.proposedBy === conversation.myRole,
        kindLabel: offerKindLabel(offer, offers),
        actions: offerActions(offer, conversation.myRole, conversation.announcementStatus),
        changedSince: offer.isOutdated ? changedFieldsLabel(offer.changesSinceOffer) : '',
      });
    } else if (entry.type === 'AnnouncementChanged' && entry.change) {
      items.push({
        kind: 'change',
        key: `c-${entry.change.revision}`,
        at: entry.at,
        fields: changedFieldsLabel(entry.change.fields),
        revision: entry.change.revision,
      });
    }
  }
  return items;
}

/** Oferty wątku (do „czy mam oczekującą ofertę”, podpowiedzi ceny). */
export function offersOf(conversation: ConversationDto): OfferDto[] {
  return conversation.timeline.filter((entry) => entry.type === 'Offer' && entry.offer).map((entry) => entry.offer);
}
