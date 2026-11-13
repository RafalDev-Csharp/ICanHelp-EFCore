import type { ConversationDto, MyConversationDto, OfferDto } from '../../../core/api/api-client';

/** Oferta pomocnika `helper-1` w ogłoszeniu `a1` (oczekująca, aktualna) do testów. */
export function offerFixture(id: string, overrides: Partial<OfferDto> = {}): OfferDto {
  return {
    id,
    announcementId: 'a1',
    helperId: 'helper-1',
    proposedBy: 'Helper',
    price: { amount: 120, currency: 'PLN' },
    proposedStart: '2026-11-20T16:00:00Z',
    proposedEnd: '2026-11-20T18:00:00Z',
    message: undefined,
    status: 'Pending',
    createdAt: '2026-11-09T10:00:00Z',
    parentOfferId: undefined,
    announcementRevision: 0,
    isOutdated: false,
    editsSinceOffer: 0,
    changesSinceOffer: [],
    ...overrides,
  };
}

/** Wątek `c1` (właściciel `owner-1`, pomocnik `helper-1`) oglądany przez pomocnika. */
export function conversationFixture(overrides: Partial<ConversationDto> = {}): ConversationDto {
  return {
    id: 'c1',
    announcementId: 'a1',
    announcementTitle: 'Cieknący kran',
    announcementStatus: 'Open',
    announcementRevision: 0,
    announcementDoneReportedAt: undefined,
    clientId: 'owner-1',
    helperId: 'helper-1',
    myRole: 'Helper',
    createdAt: '2026-11-09T09:00:00Z',
    clientLastReadAt: undefined,
    helperLastReadAt: undefined,
    unreadCount: 0,
    canWrite: true,
    timeline: [],
    ...overrides,
  };
}

/** Wiersz „moich rozmów” do testów. */
export function myConversationFixture(
  conversationId: string,
  overrides: Partial<MyConversationDto> = {},
): MyConversationDto {
  return {
    conversationId,
    announcementId: 'a1',
    title: 'Cieknący kran',
    thumbnailUrl: undefined,
    announcementStatus: 'Open',
    myRole: 'Client',
    otherParty: { id: 'helper-1', userName: 'pomocnik1' },
    lastOffer: undefined as unknown as MyConversationDto['lastOffer'],
    lastMessagePreview: undefined,
    lastActivityAt: '2026-11-09T10:00:00Z',
    unreadCount: 0,
    announcementChangedSinceLastRead: false,
    ...overrides,
  };
}
