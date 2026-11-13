import type { AnnouncementDto, LastOfferDto, MyConversationDto } from '../../core/api/api-client';

/** Grupy na zakładce „Zlecenia” – w tej kolejności. */
export type OrderGroupKey = 'negotiating' | 'accepted' | 'inProgress' | 'finished';

export const ORDER_GROUPS: readonly { key: OrderGroupKey; label: string }[] = [
  { key: 'negotiating', label: 'W trakcie negocjacji' },
  { key: 'accepted', label: 'Przyjęte' },
  { key: 'inProgress', label: 'W realizacji' },
  { key: 'finished', label: 'Zakończone' },
];

export interface OrderGroup<T> {
  key: OrderGroupKey;
  label: string;
  items: T[];
}

/** Status ogłoszenia → grupa (anulowane trafiają do „Zakończone”). */
function statusGroup(status: string): OrderGroupKey {
  switch (status) {
    case 'Open':
      return 'negotiating';
    case 'Accepted':
      return 'accepted';
    case 'InProgress':
      return 'inProgress';
    default:
      return 'finished';
  }
}

/** „Zlecam”: moje ogłoszenie wg jego statusu. */
export function clientOrderGroup(announcement: Pick<AnnouncementDto, 'status'>): OrderGroupKey {
  return statusGroup(announcement.status);
}

/**
 * „Pomagam”: mój wątek jako pomocnik. Ogłoszenie otwarte = negocjacje (także same pytania); po akceptacji liczy się,
 * czy przyjęto moją ofertę (ostatnia oferta wątku `Accepted`) – jeśli wybrano kogoś innego, wątek jest „Zakończony”.
 * F11 E: zlecenie zerwane przed startem (moja oferta `Abandoned`) – „Zakończone”, choć ogłoszenie znów jest otwarte.
 */
export function helperOrderGroup(
  conversation: Pick<MyConversationDto, 'announcementStatus' | 'lastOffer'>,
): OrderGroupKey {
  // Wątek bez ofert (same pytania) – API zwraca `lastOffer: null`, choć typ NSwag tego nie mówi.
  const lastOffer = conversation.lastOffer as LastOfferDto | null | undefined;
  if (lastOffer?.status === 'Abandoned') {
    return 'finished';
  }
  const group = statusGroup(conversation.announcementStatus);
  if (group === 'negotiating') {
    return group;
  }
  const mine = lastOffer?.status === 'Accepted';
  return mine ? group : 'finished';
}

/** Elementy w grupach (stała kolejność grup, tylko niepuste; kolejność elementów jak z API). */
export function groupOrders<T>(items: readonly T[], groupOf: (item: T) => OrderGroupKey): OrderGroup<T>[] {
  return ORDER_GROUPS.map(({ key, label }) => ({
    key,
    label,
    items: items.filter((item) => groupOf(item) === key),
  })).filter((group) => group.items.length > 0);
}
