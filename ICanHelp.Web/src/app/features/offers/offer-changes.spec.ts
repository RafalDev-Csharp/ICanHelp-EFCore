import { TestBed } from '@angular/core/testing';
import { ModalController } from '@ionic/angular';

import { fieldChangeRows } from './offer-changes';
import { OfferChangesModalComponent } from './offer-changes-modal.component';

describe('fieldChangeRows', () => {
  it('shows each changed field as "before → after" in readable form', () => {
    const names: Record<string, string> = { c1: 'Dom', s1: 'Hydraulika', c2: 'Ogród', s2: 'Koszenie' };
    const rows = fieldChangeRows(
      [
        { field: 'Budget', oldValue: '150.00 PLN', newValue: '200.00 PLN' },
        { field: 'Category', oldValue: 'c1/s1', newValue: 'c2/s2' },
        { field: 'Difficulty', oldValue: 'Easy', newValue: 'Hard' },
        { field: 'Description', oldValue: 'Kran cieknie', newValue: 'Kran i bateria' },
        { field: 'Location', oldValue: '53.8, 22.9', newValue: '53.9, 23.0' },
        { field: 'Images', oldValue: 'i1, i2', newValue: 'i2, i3, i4' },
        { field: 'Title', oldValue: undefined, newValue: 'Nowy' },
      ],
      (id) => names[id] ?? id,
    );

    expect(rows[0].label).toBe('Budżet');
    expect(rows[0].before).toMatch(/150/);
    expect(rows[0].after).toMatch(/200/);
    expect(rows[1]).toEqual({ label: 'Kategoria', before: 'Dom › Hydraulika', after: 'Ogród › Koszenie' });
    expect(rows[2]).toEqual({ label: 'Trudność', before: 'Łatwe', after: 'Trudne' });
    expect(rows[3]).toEqual({ label: 'Opis', before: 'Kran cieknie', after: 'Kran i bateria' });
    // Dokładnego miejsca nie pokazujemy – tylko, że się zmieniło.
    expect(rows[4].after).toBe('nowe miejsce na mapie');
    expect(rows[5]).toEqual({ label: 'Zdjęcia', before: '2 zdjęcia', after: '3 zdjęcia (dodano 2, usunięto 1)' });
    expect(rows[6]).toEqual({ label: 'Tytuł', before: '—', after: 'Nowy' });
  });
});

describe('OfferChangesModalComponent', () => {
  const modal = { dismiss: vi.fn(() => Promise.resolve(true)) };

  function create(withActions: boolean): HTMLElement {
    TestBed.configureTestingModule({ providers: [{ provide: ModalController, useValue: modal }] });
    const fixture = TestBed.createComponent(OfferChangesModalComponent);
    fixture.componentInstance.rows = [{ label: 'Budżet', before: '150 zł', after: '200 zł' }];
    fixture.componentInstance.withActions = withActions;
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => vi.clearAllMocks());

  it('lists the changes and returns the chosen decision', () => {
    const element = create(true);

    expect(element.querySelector('.change-before')?.textContent).toBe('150 zł');
    expect(element.querySelector('.change-after')?.textContent).toBe('200 zł');
    const buttons = [...element.querySelectorAll('ion-footer ion-button')];
    expect(buttons.map((button) => button.textContent?.trim())).toEqual([
      'Podtrzymuję ofertę',
      'Zmień ofertę',
      'Wycofaj ofertę',
    ]);

    (buttons[0] as HTMLElement).click();
    expect(modal.dismiss).toHaveBeenCalledWith('keep', 'confirm');
  });

  it('is only a preview without actions', () => {
    const element = create(false);

    expect(element.querySelector('ion-footer')).toBeNull();
  });
});
