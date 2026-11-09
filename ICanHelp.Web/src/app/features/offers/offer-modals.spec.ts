import { TestBed } from '@angular/core/testing';
import { ModalController } from '@ionic/angular';

import { OfferFormModalComponent } from './offer-form-modal.component';
import { RatingModalComponent } from './rating-modal.component';

describe('offer modals', () => {
  const modal = { dismiss: vi.fn(() => Promise.resolve(true)) };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({ providers: [{ provide: ModalController, useValue: modal }] });
  });

  it('closes the offer form with the command when valid', () => {
    const fixture = TestBed.createComponent(OfferFormModalComponent);
    fixture.componentInstance.heading = 'Kontroferta';
    fixture.componentInstance.suggestedPrice = 70;
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('ion-title')?.textContent).toContain('Kontroferta');
    fixture.componentInstance.submit();

    expect(modal.dismiss).toHaveBeenCalledWith(
      expect.objectContaining({ price: { amount: 70, currency: 'PLN' } }),
      'confirm',
    );
  });

  it('keeps the offer form open without a price', () => {
    const fixture = TestBed.createComponent(OfferFormModalComponent);
    fixture.detectChanges();

    fixture.componentInstance.submit();

    expect(modal.dismiss).not.toHaveBeenCalled();
  });

  it('requires stars before sending a rating', () => {
    const fixture = TestBed.createComponent(RatingModalComponent);
    fixture.detectChanges();
    const rating = fixture.componentInstance;

    rating.submit();
    expect(modal.dismiss).not.toHaveBeenCalled();

    rating.rate.set(4);
    rating.comment = '  Szybko i solidnie ';
    rating.submit();
    expect(modal.dismiss).toHaveBeenCalledWith({ rate: 4, comment: 'Szybko i solidnie' }, 'confirm');
  });
});
