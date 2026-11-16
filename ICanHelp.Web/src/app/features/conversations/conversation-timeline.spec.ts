import type { ConversationEntryDto } from '../../core/api/api-client';
import { conversationFixture, offerFixture } from '../offers/testing/offer-fixtures';

import { offersOf, threadNotice, toTimelineItems } from './conversation-timeline';

function entry(value: Partial<ConversationEntryDto> & Pick<ConversationEntryDto, 'type' | 'at'>): ConversationEntryDto {
  return value as ConversationEntryDto;
}

describe('toTimelineItems', () => {
  const conversation = conversationFixture({
    myRole: 'Client',
    timeline: [
      entry({
        type: 'Message',
        at: '2026-11-09T10:00:00Z',
        message: {
          id: 'm1',
          senderId: 'owner-1',
          senderRole: 'Client',
          text: 'Hej',
          sentAt: '2026-11-09T10:00:00Z',
          announcementRevision: 0,
        },
      }),
      entry({ type: 'Offer', at: '2026-11-09T11:00:00Z', offer: offerFixture('o1', { isOutdated: true }) }),
      entry({
        type: 'AnnouncementChanged',
        at: '2026-11-09T12:00:00Z',
        change: {
          revision: 1,
          changedAt: '2026-11-09T12:00:00Z',
          fields: [{ field: 'Budget', oldValue: '1', newValue: '2' }],
        },
      }),
      entry({ type: 'SomethingNew', at: '2026-11-09T13:00:00Z' }),
    ],
  });

  it('maps messages, offers with actions and announcement changes, skipping unknown entries', () => {
    const items = toTimelineItems(conversation, 'owner-1');

    expect(items.map((item) => item.kind)).toEqual(['message', 'offer', 'change']);
    expect(items[0]).toMatchObject({ kind: 'message', mine: true });

    const offer = items[1];
    expect(offer.kind === 'offer' && offer.mine).toBe(false);
    expect(offer.kind === 'offer' && offer.actions).toEqual({
      accept: false,
      reject: true,
      counter: true,
      withdraw: false,
      revise: false,
    });
    expect(offer.kind === 'offer' && offer.kindLabel).toBe('Oferta');
    // F11 F: bez „nieaktualna” – druga strona (pomocnik) ma odpowiedzieć na zmiany.
    expect(offer).toMatchObject({ stateLabel: 'Czeka na odpowiedź na zmiany', changes: 'wait', previous: false });

    expect(items[2]).toMatchObject({ kind: 'change', fields: 'budżet', revision: 1, mine: true });
  });

  it('lists offers of the thread', () => {
    expect(offersOf(conversation).map((offer) => offer.id)).toEqual(['o1']);
  });
});

describe('threadNotice', () => {
  const offerEntry = (id: string, overrides = {}): ConversationEntryDto =>
    entry({ type: 'Offer', at: '2026-11-09T11:00:00Z', offer: offerFixture(id, overrides) });

  it('asks the author of an offer before the edit to see the changes', () => {
    const helper = conversationFixture({ myRole: 'Helper', timeline: [offerEntry('o1', { isOutdated: true })] });
    expect(threadNotice(helper)).toMatchObject({
      kind: 'changes',
      text: 'Zleceniodawca zmienił ogłoszenie – zobacz zmiany',
    });

    const owner = conversationFixture({
      myRole: 'Client',
      timeline: [offerEntry('o1', { isOutdated: true, proposedBy: 'Client' })],
    });
    expect(threadNotice(owner)?.text).toBe('Zmieniłeś ogłoszenie po swojej kontrofercie – zobacz zmiany');
  });

  it('points the receiver to a changed or kept offer', () => {
    const changed = conversationFixture({
      myRole: 'Client',
      timeline: [
        offerEntry('o1', { status: 'Revised' }),
        offerEntry('o2', { parentOfferId: 'o1', price: { amount: 99, currency: 'PLN' } }),
      ],
    });
    expect(threadNotice(changed)).toMatchObject({ kind: 'revised', text: 'Pomocnik zmienił ofertę – zobacz aktualną' });

    const kept = conversationFixture({
      myRole: 'Client',
      timeline: [offerEntry('o1', { status: 'Revised' }), offerEntry('o2', { parentOfferId: 'o1' })],
    });
    expect(threadNotice(kept)?.text).toBe('Pomocnik podtrzymał ofertę po zmianach – zobacz aktualną');
  });

  it('is empty without anything waiting for the reader', () => {
    expect(threadNotice(conversationFixture({ timeline: [offerEntry('o1')] }))).toBeNull();
    // Kontroferta to nie „zmienił ofertę”.
    const countered = conversationFixture({
      myRole: 'Client',
      timeline: [
        offerEntry('o1', { status: 'Countered', proposedBy: 'Client' }),
        offerEntry('o2', { parentOfferId: 'o1' }),
      ],
    });
    expect(threadNotice(countered)).toBeNull();
    const closed = conversationFixture({
      announcementStatus: 'Accepted',
      timeline: [offerEntry('o1', { isOutdated: true })],
    });
    expect(threadNotice(closed)).toBeNull();
  });

  it('marks previous versions of a revised offer', () => {
    const items = toTimelineItems(
      conversationFixture({
        timeline: [offerEntry('o1', { status: 'Revised' }), offerEntry('o2', { parentOfferId: 'o1' })],
      }),
      'helper-1',
    );
    expect(items.map((item) => item.kind === 'offer' && item.previous)).toEqual([true, false]);
  });
});
