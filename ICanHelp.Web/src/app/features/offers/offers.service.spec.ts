import { TestBed } from '@angular/core/testing';
import { AlertController, ModalController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import type { MakeOffer } from '../../core/api/api-client';
import { AnnouncementsClient } from '../../core/api/api-client';
import { PushService } from '../../core/pwa/push.service';
import { ToastService } from '../../core/ui/toast.service';

import { OfferFormModalComponent } from './offer-form-modal.component';
import { OffersService } from './offers.service';
import { RatingModalComponent } from './rating-modal.component';
import { offerFixture } from './testing/offer-fixtures';

describe('OffersService', () => {
  const client = {
    postOffer: vi.fn(),
    postCounterOffer: vi.fn(),
    postReviseOffer: vi.fn(),
    accept: vi.fn(),
    reject: vi.fn(),
    withdraw: vi.fn(),
    start: vi.fn(),
    complete: vi.fn(),
    postRating: vi.fn(),
  };
  const toast = { success: vi.fn(), error: vi.fn(), info: vi.fn() };
  const push = { suggest: vi.fn(() => Promise.resolve()) };
  /** Wynik zamknięcia okna (modal) i rola wybrana w potwierdzeniu (alert). */
  let modalResult: { data?: unknown; role?: string } = {};
  let alertRole = 'confirm';
  const modals = {
    create: vi.fn((_options: { component: unknown; componentProps?: Record<string, unknown> }) =>
      Promise.resolve({ present: () => Promise.resolve(), onWillDismiss: () => Promise.resolve(modalResult) }),
    ),
  };
  const alerts = {
    create: vi.fn(() =>
      Promise.resolve({ present: () => Promise.resolve(), onDidDismiss: () => Promise.resolve({ role: alertRole }) }),
    ),
  };
  const command: MakeOffer = {
    price: { amount: 90, currency: 'PLN' },
    proposedStart: '2026-11-20T16:00:00.000Z',
    proposedEnd: '2026-11-20T18:00:00.000Z',
    message: undefined,
  };

  function create(): OffersService {
    TestBed.configureTestingModule({
      providers: [
        { provide: AnnouncementsClient, useValue: client },
        { provide: ModalController, useValue: modals },
        { provide: AlertController, useValue: alerts },
        { provide: ToastService, useValue: toast },
        { provide: PushService, useValue: push },
      ],
    });
    return TestBed.inject(OffersService);
  }

  beforeEach(() => {
    vi.clearAllMocks();
    modalResult = {};
    alertRole = 'confirm';
  });

  it('makes an offer from the form with the budget as a hint', async () => {
    modalResult = { data: command, role: 'confirm' };
    client.postOffer.mockReturnValue(of({ id: 'o9' }));
    const service = create();

    await expect(service.make('a1', 150)).resolves.toBe('o9');

    expect(modals.create.mock.calls[0][0].component).toBe(OfferFormModalComponent);
    expect(modals.create.mock.calls[0][0].componentProps).toMatchObject({ suggestedPrice: 150 });
    expect(client.postOffer).toHaveBeenCalledWith('a1', command);
    expect(toast.success).toHaveBeenCalledWith('Oferta wysłana.');
    expect(push.suggest).toHaveBeenCalledWith('Powiadomić Cię, gdy zleceniodawca odpowie na ofertę?');
  });

  it('sends nothing when the form is closed', async () => {
    modalResult = { role: 'cancel' };
    const service = create();

    await expect(service.make('a1')).resolves.toBeNull();
    expect(client.postOffer).not.toHaveBeenCalled();
    expect(push.suggest).not.toHaveBeenCalled();
  });

  it('counters an outdated offer with a note about the change', async () => {
    modalResult = { data: command, role: 'confirm' };
    client.postCounterOffer.mockReturnValue(of({ id: 'o2' }));
    const offer = offerFixture('o1', {
      isOutdated: true,
      changesSinceOffer: [{ field: 'Budget', oldValue: '100', newValue: '80' }],
    });
    const service = create();

    await expect(service.counter(offer)).resolves.toBe(true);

    expect(modals.create.mock.calls[0][0].componentProps?.['note']).toContain('budżet');
    expect(client.postCounterOffer).toHaveBeenCalledWith('a1', 'o1', command);
  });

  it('revises own outdated offer from its terms, with a note about the change', async () => {
    modalResult = { data: command, role: 'confirm' };
    client.postReviseOffer.mockReturnValue(of({ id: 'o2' }));
    const offer = offerFixture('o1', {
      isOutdated: true,
      changesSinceOffer: [{ field: 'Description', oldValue: 'a', newValue: 'b' }],
    });
    const service = create();

    await expect(service.revise(offer)).resolves.toBe(true);

    const props = modals.create.mock.calls[0][0].componentProps;
    expect(props?.['heading']).toBe('Zaktualizuj ofertę');
    expect(props?.['basis']).toBe(offer);
    expect(props?.['note']).toContain('opis');
    expect(client.postReviseOffer).toHaveBeenCalledWith('a1', 'o1', command);
    expect(toast.success).toHaveBeenCalledWith('Oferta zaktualizowana.');
  });

  it('does not revise when the form is closed', async () => {
    modalResult = { role: 'cancel' };
    const service = create();

    await expect(service.revise(offerFixture('o1', { isOutdated: true }))).resolves.toBe(false);
    expect(client.postReviseOffer).not.toHaveBeenCalled();
  });

  it('accepts only after confirmation', async () => {
    client.accept.mockReturnValue(of(undefined));
    const service = create();

    alertRole = 'cancel';
    await expect(service.accept(offerFixture('o1'))).resolves.toBe(false);
    expect(client.accept).not.toHaveBeenCalled();

    alertRole = 'confirm';
    await expect(service.accept(offerFixture('o1'))).resolves.toBe(true);
    expect(client.accept).toHaveBeenCalledWith('a1', 'o1');
    expect(toast.success).toHaveBeenCalledWith('Oferta przyjęta.');
  });

  it('starts and completes the job only after confirmation', async () => {
    client.start.mockReturnValue(of(undefined));
    client.complete.mockReturnValue(of(undefined));
    const service = create();

    alertRole = 'cancel';
    await expect(service.start('a1')).resolves.toBe(false);
    expect(client.start).not.toHaveBeenCalled();

    alertRole = 'confirm';
    await expect(service.start('a1')).resolves.toBe(true);
    expect(client.start).toHaveBeenCalledWith('a1');
    expect(toast.success).toHaveBeenCalledWith('Zlecenie w realizacji.');

    await expect(service.complete('a1')).resolves.toBe(true);
    expect(client.complete).toHaveBeenCalledWith('a1');
    expect(toast.success).toHaveBeenCalledWith('Zlecenie zakończone. Oceń współpracę!');
  });

  it('reports failure when the API refuses (message shown by the interceptor)', async () => {
    alertRole = 'destructive';
    client.withdraw.mockReturnValue(throwError(() => ({ status: 409 })));
    const service = create();

    await expect(service.withdraw(offerFixture('o1'))).resolves.toBe(false);
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('rates the other side from the rating window', async () => {
    modalResult = { data: { rate: 5, comment: 'Polecam' }, role: 'confirm' };
    client.postRating.mockReturnValue(of({ id: 'r1' }));
    const service = create();

    await expect(service.rate('a1', 'Jan')).resolves.toBe(true);
    expect(modals.create.mock.calls[0][0].component).toBe(RatingModalComponent);
    expect(client.postRating).toHaveBeenCalledWith('a1', { rate: 5, comment: 'Polecam' });
  });
});
