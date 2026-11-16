import { myConversationFixture, offerFixture } from '../../offers/testing/offer-fixtures';
import { announcementDetails } from '../testing/announcement-fixtures';

import { negotiationsOf, ownerHistory } from './announcement-negotiations';

describe('negotiationsOf', () => {
  it('keeps the latest offer per helper, joins threads and puts pending first', () => {
    const offers = [
      offerFixture('a-1', { helperId: 'h-a', status: 'Countered', createdAt: '2026-11-09T08:00:00Z' }),
      offerFixture('a-2', { helperId: 'h-a', status: 'Rejected', createdAt: '2026-11-09T12:00:00Z' }),
      offerFixture('b-1', { helperId: 'h-b', status: 'Pending', createdAt: '2026-11-09T09:00:00Z' }),
    ];
    const conversations = [
      myConversationFixture('c-a', { otherParty: { id: 'h-a', userName: 'ala' } }),
      myConversationFixture('c-b', { otherParty: { id: 'h-b', userName: 'bob' }, unreadCount: 2 }),
      myConversationFixture('c-c', { otherParty: { id: 'h-c', userName: 'cezary' } }),
    ];

    const result = negotiationsOf(offers, conversations, 'owner-1');

    expect(result.map((n) => n.helperId)).toEqual(['h-b', 'h-a', 'h-c']);
    expect(result[0]).toMatchObject({ userName: 'bob', conversationId: 'c-b', unreadCount: 2 });
    expect(result[1].latest?.id).toBe('a-2');
    expect(result[2].latest).toBeUndefined();
  });

  it('for a helper joins his only thread to his offers', () => {
    const result = negotiationsOf(
      [offerFixture('o1')],
      [myConversationFixture('c1', { myRole: 'Helper', otherParty: { id: 'owner-1', userName: 'jan' } })],
      'helper-1',
    );

    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({ helperId: 'helper-1', conversationId: 'c1', userName: 'Ty' });
  });
});

describe('ownerHistory', () => {
  it('lists offers, counter-offers and edits from the newest', () => {
    const announcement = announcementDetails('a1', {
      offers: [
        offerFixture('o1', { createdAt: '2026-11-09T08:00:00Z', status: 'Countered' }),
        offerFixture('o2', {
          createdAt: '2026-11-09T10:00:00Z',
          proposedBy: 'Client',
          parentOfferId: 'o1',
          price: { amount: 100, currency: 'PLN' },
        }),
      ],
      changes: [
        { revision: 1, changedAt: '2026-11-09T09:00:00Z', fields: [{ field: 'Title', oldValue: 'a', newValue: 'b' }] },
      ],
    });

    const history = ownerHistory(announcement, new Map([['helper-1', 'ala']]));

    expect(history.map((entry) => entry.key)).toEqual(['o-o2', 'c-1', 'o-o1']);
    expect(history[0].text.replace(/\s/g, ' ')).toBe('Kontroferta (Ty): 100 zł – czeka na odpowiedź');
    expect(history[1].text).toBe('Edycja: tytuł');
    expect(history[2].text).toContain('Oferta (ala)');
  });
});
