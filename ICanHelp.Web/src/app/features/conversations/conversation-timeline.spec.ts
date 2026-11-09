import type { ConversationEntryDto } from '../../core/api/api-client';
import { conversationFixture, offerFixture } from '../offers/testing/offer-fixtures';

import { offersOf, toTimelineItems } from './conversation-timeline';

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
    });

    expect(items[2]).toMatchObject({ kind: 'change', fields: 'budżet', revision: 1 });
  });

  it('lists offers of the thread', () => {
    expect(offersOf(conversation).map((offer) => offer.id)).toEqual(['o1']);
  });
});
