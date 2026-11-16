import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ModalController } from '@ionic/angular';

import { ModalService } from './modal.service';

@Component({ selector: 'app-modal-test-window', template: '' })
class TestWindowComponent {}

describe('ModalService', () => {
  /** Jak użytkownik zamyka okno: rola i dane z `dismiss`. */
  let dismissed: { role?: string; data?: unknown } = {};
  const controller = {
    create: vi.fn((_options: { component: unknown; componentProps?: Record<string, unknown> }) =>
      Promise.resolve({ present: () => Promise.resolve(), onWillDismiss: () => Promise.resolve(dismissed) }),
    ),
  };
  let modals: ModalService;

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({ providers: [{ provide: ModalController, useValue: controller }] });
    modals = TestBed.inject(ModalService);
  });

  it('opens a window with a component and returns the confirmed data', async () => {
    dismissed = { role: 'confirm', data: { rate: 5 } };

    await expect(modals.open(TestWindowComponent, { otherParty: 'Jan' })).resolves.toEqual({ rate: 5 });
    expect(controller.create.mock.calls[0][0]).toEqual({
      component: TestWindowComponent,
      componentProps: { otherParty: 'Jan' },
    });
  });

  it('returns nothing when the window was cancelled', async () => {
    dismissed = { role: 'cancel', data: { rate: 5 } };
    await expect(modals.open(TestWindowComponent)).resolves.toBeNull();

    dismissed = { role: 'confirm' };
    await expect(modals.open(TestWindowComponent)).resolves.toBeNull();
  });
});
