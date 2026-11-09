import type { AnnouncementDetailsDto, MyConversationDto, OfferDto } from '../../../core/api/api-client';
import { formatPrice } from '../../../shared/pipes/price.pipe';
import { parseApiDate } from '../../../shared/pipes/time-ago.pipe';
import { changedFieldsLabel, offerStatusLabel } from '../../offers/offer-labels';

/** Negocjacja z jednym pomocnikiem w szczegółach ogłoszenia: ostatnia oferta + wątek (jeśli jest). */
export interface Negotiation {
  helperId: string;
  /** Nazwa z wątku (`otherParty` / dla pomocnika – „Ty”); bez wątku – pusta. */
  userName: string;
  conversationId: string | undefined;
  /** Ostatnia oferta w łańcuchu (oferta → kontroferty), `undefined` = na razie same wiadomości. */
  latest: OfferDto | undefined;
  unreadCount: number;
}

function time(value: string): number {
  return parseApiDate(value).getTime();
}

/**
 * Negocjacje per pomocnik: oferty widoczne dla zalogowanego (właściciel – wszystkie, pomocnik – swoje) połączone
 * z jego wątkami (`GET api/me/conversations?announcementId=`). Najpierw te, które czekają na odpowiedź, potem od
 * ostatniej aktywności.
 */
export function negotiationsOf(
  offers: readonly OfferDto[],
  conversations: readonly MyConversationDto[],
  myId: string | undefined,
): Negotiation[] {
  const byHelper = new Map<string, Negotiation>();
  const entry = (helperId: string): Negotiation => {
    let negotiation = byHelper.get(helperId);
    if (!negotiation) {
      negotiation = { helperId, userName: '', conversationId: undefined, latest: undefined, unreadCount: 0 };
      byHelper.set(helperId, negotiation);
    }
    return negotiation;
  };

  for (const offer of offers) {
    const negotiation = entry(offer.helperId);
    if (!negotiation.latest || time(offer.createdAt) > time(negotiation.latest.createdAt)) {
      negotiation.latest = offer;
    }
  }
  for (const conversation of conversations) {
    // Właściciel: druga strona = pomocnik. Pomocnik: wątek jest jego, druga strona = właściciel.
    const helperId = conversation.myRole === 'Client' ? conversation.otherParty.id : (myId ?? '');
    const negotiation = entry(helperId);
    negotiation.conversationId = conversation.conversationId;
    negotiation.userName = conversation.myRole === 'Client' ? conversation.otherParty.userName : 'Ty';
    negotiation.unreadCount = conversation.unreadCount;
  }

  const lastActivity = (negotiation: Negotiation): number => {
    const conversation = conversations.find((c) => c.conversationId === negotiation.conversationId);
    return Math.max(
      negotiation.latest ? time(negotiation.latest.createdAt) : 0,
      conversation ? time(conversation.lastActivityAt) : 0,
    );
  };
  const pending = (negotiation: Negotiation): number => (negotiation.latest?.status === 'Pending' ? 0 : 1);

  return [...byHelper.values()].sort((a, b) => pending(a) - pending(b) || lastActivity(b) - lastActivity(a));
}

/** Wpis „Historii” ogłoszenia (właściciel): oferty, kontroferty i edycje, od najnowszych. */
export interface HistoryEntry {
  key: string;
  at: string;
  kind: 'offer' | 'change';
  text: string;
}

export function ownerHistory(announcement: AnnouncementDetailsDto, names: ReadonlyMap<string, string>): HistoryEntry[] {
  const offers: HistoryEntry[] = announcement.offers.map((offer) => {
    const who = offer.proposedBy === 'Client' ? 'Ty' : names.get(offer.helperId) || 'pomocnik';
    const kind = offer.parentOfferId ? 'Kontroferta' : 'Oferta';
    return {
      key: `o-${offer.id}`,
      at: offer.createdAt,
      kind: 'offer',
      text: `${kind} (${who}): ${formatPrice(offer.price.amount)} – ${offerStatusLabel(offer.status).toLowerCase()}`,
    };
  });
  const changes: HistoryEntry[] = announcement.changes.map((change) => ({
    key: `c-${change.revision}`,
    at: change.changedAt,
    kind: 'change',
    text: `Edycja: ${changedFieldsLabel(change.fields) || 'treść'}`,
  }));
  return [...offers, ...changes].sort((a, b) => time(b.at) - time(a.at));
}
