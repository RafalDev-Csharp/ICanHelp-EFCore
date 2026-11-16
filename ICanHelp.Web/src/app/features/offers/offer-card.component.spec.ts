import { TestBed } from '@angular/core/testing';

import { OfferCardComponent } from './offer-card.component';
import type { OfferAction } from './offer-view';
import { offerView } from './offer-view';
import { offerFixture } from './testing/offer-fixtures';

describe('OfferCardComponent', () => {
  function create(
    view: ReturnType<typeof offerView>,
    inputs: { busy?: boolean; highlighted?: boolean } = {},
  ): { element: HTMLElement; actions: OfferAction[] } {
    const fixture = TestBed.createComponent(OfferCardComponent);
    fixture.componentRef.setInput('view', view);
    fixture.componentRef.setInput('at', '2026-11-09T11:00:00Z');
    fixture.componentRef.setInput('busy', inputs.busy ?? false);
    fixture.componentRef.setInput('highlighted', inputs.highlighted ?? false);
    const actions: OfferAction[] = [];
    fixture.componentInstance.action.subscribe((action) => actions.push(action));
    fixture.detectChanges();
    return { element: fixture.nativeElement as HTMLElement, actions };
  }

  it('shows the price, the term, the message and the state of an offer', () => {
    const offer = offerFixture('o1', { message: 'Mam własne narzędzia.' });
    const { element } = create(offerView(offer, [offer], 'Client', 'Open'), { highlighted: true });

    expect(element.querySelector('article.offer')?.id).toBe('offer-o1');
    expect(element.querySelector('article.offer')?.classList).toContain('highlight');
    expect(element.querySelector('.offer-title')?.textContent).toContain('Oferta · Pomocnik');
    expect(element.querySelector('.offer-price')?.textContent?.replace(/\s/g, ' ')).toContain('120 zł');
    expect(element.querySelector('.offer-message')?.textContent).toContain('Mam własne narzędzia.');
    expect(element.querySelector('ion-badge')?.textContent).toContain('Czeka na odpowiedź');
  });

  it('reports the clicked action and blocks the buttons while another action runs', () => {
    const offer = offerFixture('o1');
    const view = offerView(offer, [offer], 'Client', 'Open');
    const { element, actions } = create(view);
    const buttons = [...element.querySelectorAll<HTMLElement>('.offer-actions ion-button')];

    expect(buttons.map((button) => button.textContent?.trim())).toEqual(['Odrzuć', 'Kontroferta', 'Przyjmij']);
    buttons[2].dispatchEvent(new Event('click'));
    buttons[0].dispatchEvent(new Event('click'));
    expect(actions).toEqual(['accept', 'reject']);

    TestBed.resetTestingModule();
    const busy = create(view, { busy: true });
    const disabled = [...busy.element.querySelectorAll('.offer-actions ion-button')].map(
      (button) => (button as unknown as { disabled: boolean }).disabled,
    );
    expect(disabled).toEqual([true, true, true]);
  });

  it('shows the note about changes of the announcement with a single way forward', () => {
    const offer = offerFixture('o1', {
      isOutdated: true,
      changesSinceOffer: [{ field: 'Budget', oldValue: '100.00 PLN', newValue: '150.00 PLN' }],
    });
    const { element, actions } = create(offerView(offer, [offer], 'Helper', 'Open'));

    expect(element.querySelector('article.offer')?.classList).toContain('awaiting');
    expect(element.querySelector('.offer-changes')?.textContent).toContain('Zleceniodawca zmienił ogłoszenie');
    const buttons = element.querySelectorAll('.offer-actions ion-button');
    expect(buttons).toHaveLength(1);
    buttons[0].dispatchEvent(new Event('click'));
    expect(actions).toEqual(['reviewChanges']);
  });

  it('collapses a previous version of the offer', () => {
    const previous = offerFixture('o1', { status: 'Revised' });
    const { element } = create(offerView(previous, [previous], 'Client', 'Open'));

    expect(element.querySelector('details.offer.previous summary')?.textContent).toContain(
      'Poprzednia wersja · Pomocnik',
    );
    expect(element.querySelector('article.offer')).toBeNull();
    expect(element.querySelector('.offer-actions')).toBeNull();
  });
});
