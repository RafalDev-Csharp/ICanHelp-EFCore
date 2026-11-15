import type { ConversationEntryDto } from '../../core/api/api-client';
import { conversationFixture, offerFixture } from '../offers/testing/offer-fixtures';

import {
  canMakeOffer,
  isQuestionsOnly,
  messagePlaceholder,
  otherPartyId,
  readOnlyReason,
  threadJobStep,
} from './conversation-thread';

const offerEntry = (overrides = {}): ConversationEntryDto =>
  ({ type: 'Offer', at: '2026-11-09T11:00:00Z', offer: offerFixture('o1', overrides) }) as ConversationEntryDto;

const exitEntry = (kind: string): ConversationEntryDto =>
  ({
    type: 'JobExit',
    at: '2026-11-10T10:00:00Z',
    jobExit: { number: 1, kind, offerId: 'o1', helperId: 'helper-1', actorId: 'owner-1', actorRole: 'Client' },
  }) as ConversationEntryDto;

describe('conversation thread rules', () => {
  it('finds the other party of the thread', () => {
    expect(otherPartyId(conversationFixture({ myRole: 'Helper' }))).toBe('owner-1');
    expect(otherPartyId(conversationFixture({ myRole: 'Client' }))).toBe('helper-1');
  });

  it('treats a thread without offers as questions and answers', () => {
    const questions = conversationFixture({ myRole: 'Helper' });
    expect(isQuestionsOnly(questions)).toBe(true);
    expect(messagePlaceholder(questions)).toBe('Zadaj pytanie…');
    expect(messagePlaceholder(conversationFixture({ myRole: 'Client' }))).toBe('Odpowiedz…');

    const negotiation = conversationFixture({ timeline: [offerEntry()] });
    expect(isQuestionsOnly(negotiation)).toBe(false);
    expect(messagePlaceholder(negotiation)).toBe('Napisz wiadomość…');
  });

  it('lets a helper make an offer only in an open announcement, without a pending offer or a broken job', () => {
    expect(canMakeOffer(conversationFixture())).toBe(true);
    expect(canMakeOffer(conversationFixture({ timeline: [offerEntry({ status: 'Rejected' })] }))).toBe(true);
    expect(canMakeOffer(conversationFixture({ timeline: [offerEntry()] }))).toBe(false);
    expect(canMakeOffer(conversationFixture({ myRole: 'Client' }))).toBe(false);
    expect(canMakeOffer(conversationFixture({ announcementStatus: 'Accepted' }))).toBe(false);
    expect(
      canMakeOffer(conversationFixture({ timeline: [offerEntry({ status: 'Abandoned' }), exitEntry('Abandoned')] })),
    ).toBe(false);
  });

  it('has a job step only in the thread of the accepted helper', () => {
    const accepted = [offerEntry({ status: 'Accepted' })];
    expect(threadJobStep(conversationFixture({ announcementStatus: 'Accepted', timeline: accepted }))?.action).toBe(
      'start',
    );
    expect(
      threadJobStep(
        conversationFixture({ announcementStatus: 'Accepted', timeline: [offerEntry({ status: 'Rejected' })] }),
      ),
    ).toBeNull();
    // Po zakończeniu nie ma już nic do zrobienia ani do pokazania.
    expect(threadJobStep(conversationFixture({ announcementStatus: 'Completed', timeline: accepted }))).toBeNull();
  });

  it('explains why the thread is read-only', () => {
    expect(readOnlyReason(conversationFixture({ announcementStatus: 'Accepted' }))).toContain('przyjął inną ofertę');
    expect(readOnlyReason(conversationFixture({ announcementStatus: 'Completed' }))).toContain('Zlecenie zakończone');
    expect(readOnlyReason(conversationFixture({ announcementStatus: 'Cancelled' }))).toContain('anulowane');
    expect(readOnlyReason(conversationFixture({ timeline: [exitEntry('Abandoned')] }))).toContain(
      'Zlecenie zerwane przed rozpoczęciem',
    );
    expect(
      readOnlyReason(conversationFixture({ announcementStatus: 'Interrupted', timeline: [exitEntry('Interrupted')] })),
    ).toContain('Zlecenie przerwane');
    expect(readOnlyReason(null)).toBe('Rozmowa jest tylko do odczytu.');
  });
});
