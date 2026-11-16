import { TestBed } from '@angular/core/testing';
import { AlertController, ModalController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import { AnnouncementsClient } from '../../core/api/api-client';
import { ToastService } from '../../core/ui/toast.service';

import { JobExitModalComponent } from './job-exit-modal.component';
import { JobService } from './job.service';
import { RatingModalComponent } from './rating-modal.component';

describe('JobService', () => {
  const client = {
    start: vi.fn(),
    reportDone: vi.fn(),
    complete: vi.fn(),
    postRating: vi.fn(),
    abandon: vi.fn(),
    interrupt: vi.fn(),
    respondToJobExit: vi.fn(),
  };
  const toast = { success: vi.fn(), error: vi.fn(), info: vi.fn() };
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

  function create(): JobService {
    TestBed.configureTestingModule({
      providers: [
        { provide: AnnouncementsClient, useValue: client },
        { provide: ModalController, useValue: modals },
        { provide: AlertController, useValue: alerts },
        { provide: ToastService, useValue: toast },
      ],
    });
    return TestBed.inject(JobService);
  }

  beforeEach(() => {
    vi.clearAllMocks();
    modalResult = {};
    alertRole = 'confirm';
  });

  it('starts, reports and completes the job only after confirmation', async () => {
    client.start.mockReturnValue(of(undefined));
    client.reportDone.mockReturnValue(of(undefined));
    client.complete.mockReturnValue(of(undefined));
    const service = create();

    alertRole = 'cancel';
    await expect(service.run('a1', 'start')).resolves.toBe(false);
    expect(client.start).not.toHaveBeenCalled();

    alertRole = 'confirm';
    await expect(service.run('a1', 'start')).resolves.toBe(true);
    expect(client.start).toHaveBeenCalledWith('a1');
    expect(toast.success).toHaveBeenCalledWith('Zlecenie w realizacji.');

    await expect(service.run('a1', 'report')).resolves.toBe(true);
    expect(client.reportDone).toHaveBeenCalledWith('a1');
    expect(toast.success).toHaveBeenCalledWith('Wykonanie zgłoszone – czekasz na potwierdzenie.');

    await expect(service.run('a1', 'complete')).resolves.toBe(true);
    expect(client.complete).toHaveBeenCalledWith('a1');
    expect(toast.success).toHaveBeenCalledWith('Zlecenie zakończone. Oceń współpracę!');
  });

  it('reports failure when the API refuses (message shown by the interceptor)', async () => {
    client.start.mockReturnValue(throwError(() => ({ status: 409 })));
    const service = create();

    await expect(service.run('a1', 'start')).resolves.toBe(false);
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

  it('abandons or interrupts the job with the reason from the window', async () => {
    client.abandon.mockReturnValue(of(undefined));
    client.interrupt.mockReturnValue(of(undefined));
    const service = create();

    modalResult = { data: { reason: 'CannotMakeIt', note: undefined }, role: 'confirm' };
    await expect(service.exit('a1', 'abandon', 'Zleceniodawca')).resolves.toBe(true);
    expect(modals.create.mock.calls[0][0].component).toBe(JobExitModalComponent);
    expect(modals.create.mock.calls[0][0].componentProps).toEqual({ action: 'abandon', otherSide: 'Zleceniodawca' });
    expect(client.abandon).toHaveBeenCalledWith('a1', { reason: 'CannotMakeIt', note: undefined });

    modalResult = { data: { reason: 'Disagreement', note: 'Inny zakres.' }, role: 'confirm' };
    await expect(service.exit('a1', 'interrupt', 'Pomocnik')).resolves.toBe(true);
    expect(client.interrupt).toHaveBeenCalledWith('a1', { reason: 'Disagreement', note: 'Inny zakres.' });
  });

  it('sends the statement from the text window', async () => {
    client.respondToJobExit.mockReturnValue(of(undefined));
    alerts.create.mockImplementationOnce(() =>
      Promise.resolve({
        present: () => Promise.resolve(),
        onDidDismiss: () => Promise.resolve({ role: 'confirm', data: { values: { text: '  Inny zakres.  ' } } }),
      }),
    );
    const service = create();

    await expect(service.respondToExit('a1', 2)).resolves.toBe(true);
    expect(client.respondToJobExit).toHaveBeenCalledWith('a1', 2, { text: 'Inny zakres.' });
  });

  it('does not send an empty statement', async () => {
    alerts.create.mockImplementationOnce(() =>
      Promise.resolve({
        present: () => Promise.resolve(),
        onDidDismiss: () => Promise.resolve({ role: 'confirm', data: { values: { text: ' ' } } }),
      }),
    );
    const service = create();

    await expect(service.respondToExit('a1', 1)).resolves.toBe(false);
    expect(client.respondToJobExit).not.toHaveBeenCalled();
  });

  it('does not abandon when the reason window is closed', async () => {
    modalResult = { role: 'cancel' };
    const service = create();

    await expect(service.exit('a1', 'abandon', 'Pomocnik')).resolves.toBe(false);
    expect(client.abandon).not.toHaveBeenCalled();
  });
});
