import { TestBed } from '@angular/core/testing';
import { ModalController } from '@ionic/angular';

import { JobExitModalComponent } from './job-exit-modal.component';
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

  it('requires a reason – and a note for "other" – before abandoning a job', () => {
    const fixture = TestBed.createComponent(JobExitModalComponent);
    fixture.componentInstance.action = 'abandon';
    fixture.detectChanges();
    const exit = fixture.componentInstance;
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('ion-title')?.textContent).toContain('Rezygnacja');
    expect(element.querySelectorAll('ion-radio')).toHaveLength(4);
    exit.submit();
    expect(modal.dismiss).not.toHaveBeenCalled();

    exit.reason.set('Other');
    exit.submit();
    expect(modal.dismiss).not.toHaveBeenCalled();

    exit.note = ' Sam naprawiłem. ';
    exit.submit();
    expect(modal.dismiss).toHaveBeenCalledWith({ reason: 'Other', note: 'Sam naprawiłem.' }, 'confirm');
  });

  it('offers "conflict" when interrupting and sends the reason without a note', () => {
    const fixture = TestBed.createComponent(JobExitModalComponent);
    fixture.componentInstance.action = 'interrupt';
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Konflikt / nie dogadaliśmy się');
    fixture.componentInstance.reason.set('Disagreement');
    fixture.componentInstance.submit();
    expect(modal.dismiss).toHaveBeenCalledWith({ reason: 'Disagreement', note: undefined }, 'confirm');
  });
});
