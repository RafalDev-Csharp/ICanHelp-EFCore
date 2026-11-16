import { offerActions } from './offer-labels';
import { offerButtons, offerView } from './offer-view';
import { offerFixture } from './testing/offer-fixtures';

describe('offerView', () => {
  const budgetChanged = [{ field: 'Budget', oldValue: '100.00 PLN', newValue: '150.00 PLN' }];

  it('describes an offer of the other side with its actions', () => {
    const offer = offerFixture('o1');
    const view = offerView(offer, [offer], 'Client', 'Open');

    expect(view).toMatchObject({
      mine: false,
      author: 'Pomocnik',
      kindLabel: 'Oferta',
      stateLabel: 'Czeka na odpowiedź',
      changes: null,
      changesNote: '',
      previous: false,
    });
    expect(view.buttons.map((button) => button.label)).toEqual(['Odrzuć', 'Kontroferta', 'Przyjmij']);
  });

  it('asks the author of an offer made before an edit to see the changes first', () => {
    const offer = offerFixture('o1', { isOutdated: true, changesSinceOffer: budgetChanged });
    const view = offerView(offer, [offer], 'Helper', 'Open');

    expect(view).toMatchObject({ mine: true, author: 'Ty', changes: 'respond', changedSince: 'budżet' });
    expect(view.changesNote).toBe(
      'Zleceniodawca zmienił ogłoszenie po tej ofercie (budżet). Zobacz zmiany i podtrzymaj ofertę, zmień ją albo wycofaj.',
    );
    expect(view.buttons).toEqual([{ action: 'reviewChanges', label: 'Zobacz zmiany', icon: 'eye-outline' }]);
  });

  it('tells the other side that the author has to answer the changes', () => {
    const offer = offerFixture('o1', { isOutdated: true, changesSinceOffer: budgetChanged });
    const view = offerView(offer, [offer], 'Client', 'Open');

    expect(view.changes).toBe('wait');
    expect(view.changesNote).toBe('Czeka na odpowiedź pomocnika na zmiany w ogłoszeniu (budżet).');
    // Oferty sprzed edycji nie da się przyjąć – kontroferta zostaje główną akcją.
    expect(view.buttons.map((button) => [button.label, button.fill])).toEqual([
      ['Odrzuć', 'outline'],
      ['Zaproponuj kontrofertę', 'solid'],
    ]);
  });

  it('speaks to the owner about his own counter-offer made before his edit', () => {
    const counter = offerFixture('o2', { proposedBy: 'Client', isOutdated: true, changesSinceOffer: [] });

    expect(offerView(counter, [counter], 'Client', 'Open').changesNote).toBe(
      'Zmieniłeś ogłoszenie po tej ofercie. Zobacz zmiany i podtrzymaj ofertę, zmień ją albo wycofaj.',
    );
  });

  it('collapses a replaced version and names the new one', () => {
    const previous = offerFixture('o1', { status: 'Revised' });
    const current = offerFixture('o2', { parentOfferId: 'o1' });

    expect(offerView(previous, [previous, current], 'Client', 'Open')).toMatchObject({ previous: true, buttons: [] });
    expect(offerView(current, [previous, current], 'Client', 'Open').kindLabel).toBe('Zaktualizowana oferta');
  });

  it('has no notes and no buttons once the announcement is closed', () => {
    const offer = offerFixture('o1', { isOutdated: true, changesSinceOffer: budgetChanged });

    expect(offerView(offer, [offer], 'Helper', 'Accepted')).toMatchObject({
      changes: null,
      changedSince: '',
      buttons: [],
    });
  });
});

describe('offerButtons', () => {
  it('lets the author withdraw or change his pending offer', () => {
    const actions = offerActions(offerFixture('o1'), 'Helper', 'Open');

    expect(offerButtons(actions, null)).toEqual([
      { action: 'withdraw', label: 'Wycofaj', fill: 'outline', color: 'medium' },
      { action: 'revise', label: 'Zmień ofertę', fill: 'outline' },
    ]);
  });
});
