import { changedFieldsLabel, formatOfferTerm, offerActions, offerStatusLabel, pendingOffer } from './offer-labels';
import { offerFixture } from './testing/offer-fixtures';

describe('offerActions', () => {
  it('lets the other side accept, reject and counter a pending offer', () => {
    expect(offerActions(offerFixture('o1'), 'Client', 'Open')).toEqual({
      accept: true,
      reject: true,
      counter: true,
      withdraw: false,
    });
  });

  it('lets only the author withdraw', () => {
    expect(offerActions(offerFixture('o1'), 'Helper', 'Open')).toEqual({
      accept: false,
      reject: false,
      counter: false,
      withdraw: true,
    });
  });

  it('replaces accept with a counter-offer when the offer is outdated', () => {
    const actions = offerActions(offerFixture('o1', { isOutdated: true }), 'Client', 'Open');
    expect(actions.accept).toBe(false);
    expect(actions.counter).toBe(true);
  });

  it('allows nothing on answered offers or closed announcements', () => {
    const none = { accept: false, reject: false, counter: false, withdraw: false };
    expect(offerActions(offerFixture('o1', { status: 'Rejected' }), 'Client', 'Open')).toEqual(none);
    expect(offerActions(offerFixture('o1'), 'Client', 'Accepted')).toEqual(none);
  });
});

describe('offer texts', () => {
  it('translates statuses and changed fields', () => {
    expect(offerStatusLabel('Countered')).toBe('Kontroferta');
    expect(offerStatusLabel('Something')).toBe('Something');
    expect(
      changedFieldsLabel([
        { field: 'Budget', oldValue: '1', newValue: '2' },
        { field: 'Description', oldValue: 'a', newValue: 'b' },
        { field: 'Budget', oldValue: '2', newValue: '3' },
      ]),
    ).toBe('budżet, opis');
  });

  it('formats a term within one day and across days', () => {
    // Czas lokalny, żeby wynik nie zależał od strefy maszyny.
    const start = new Date(2026, 10, 10, 17, 0).toISOString();
    const sameDay = new Date(2026, 10, 10, 19, 30).toISOString();
    const nextDay = new Date(2026, 10, 11, 9, 0).toISOString();

    expect(formatOfferTerm(start, sameDay)).toMatch(/10 lis.*17:00–19:30$/);
    expect(formatOfferTerm(start, nextDay)).toMatch(/10 lis.*17:00 – .*11 lis.*09:00$/);
  });

  it('finds the pending offer', () => {
    const offers = [offerFixture('o1', { status: 'Countered' }), offerFixture('o2')];
    expect(pendingOffer(offers)?.id).toBe('o2');
    expect(pendingOffer([offerFixture('o3', { status: 'Withdrawn' })])).toBeUndefined();
  });
});
