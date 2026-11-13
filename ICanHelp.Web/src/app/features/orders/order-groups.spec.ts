import type { MyConversationDto } from '../../core/api/api-client';
import { announcement } from '../announcements/testing/announcement-fixtures';
import { myConversationFixture } from '../offers/testing/offer-fixtures';

import { clientOrderGroup, groupOrders, helperOrderGroup } from './order-groups';

const offer = (status: string): MyConversationDto['lastOffer'] => ({
  id: 'o1',
  price: { amount: 100, currency: 'PLN' },
  status,
  proposedBy: 'Helper',
  isOutdated: false,
  createdAt: '2026-11-09T10:00:00Z',
});

describe('order groups', () => {
  it('groups my announcements by status, cancelled with finished', () => {
    expect(clientOrderGroup({ status: 'Open' })).toBe('negotiating');
    expect(clientOrderGroup({ status: 'Accepted' })).toBe('accepted');
    expect(clientOrderGroup({ status: 'InProgress' })).toBe('inProgress');
    expect(clientOrderGroup({ status: 'Completed' })).toBe('finished');
    expect(clientOrderGroup({ status: 'Cancelled' })).toBe('finished');
    expect(clientOrderGroup({ status: 'Interrupted' })).toBe('finished');
  });

  it('finishes a helper thread whose job was abandoned, although the announcement is open again', () => {
    expect(
      helperOrderGroup(myConversationFixture('c1', { announcementStatus: 'Open', lastOffer: offer('Abandoned') })),
    ).toBe('finished');
    expect(
      helperOrderGroup(
        myConversationFixture('c2', { announcementStatus: 'Interrupted', lastOffer: offer('Accepted') }),
      ),
    ).toBe('finished');
  });

  it('puts a helper thread in a job group only when my offer was accepted', () => {
    expect(helperOrderGroup(myConversationFixture('c1', { announcementStatus: 'Open' }))).toBe('negotiating');
    expect(
      helperOrderGroup(myConversationFixture('c2', { announcementStatus: 'InProgress', lastOffer: offer('Accepted') })),
    ).toBe('inProgress');
    expect(
      helperOrderGroup(myConversationFixture('c3', { announcementStatus: 'Accepted', lastOffer: offer('Rejected') })),
    ).toBe('finished');
    // Same pytania, ogłoszenie przyjęte przez kogoś innego.
    expect(helperOrderGroup(myConversationFixture('c4', { announcementStatus: 'Accepted' }))).toBe('finished');
  });

  it('returns only non-empty groups in a fixed order', () => {
    const items = [
      announcement('a1', { status: 'Completed' }),
      announcement('a2', { status: 'Open' }),
      announcement('a3', { status: 'Open' }),
    ];

    const groups = groupOrders(items, clientOrderGroup);

    expect(groups.map((group) => [group.label, group.items.map((item) => item.id)])).toEqual([
      ['W trakcie negocjacji', ['a2', 'a3']],
      ['Zakończone', ['a1']],
    ]);
  });
});
