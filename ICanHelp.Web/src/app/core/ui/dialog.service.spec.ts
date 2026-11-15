import { TestBed } from '@angular/core/testing';
import { AlertController } from '@ionic/angular';

import { DialogService } from './dialog.service';
import { ToastService } from './toast.service';

interface AlertButton {
  text: string;
  role?: string;
  handler?: (values: { text?: string }) => boolean;
}

describe('DialogService', () => {
  const toast = { success: vi.fn(), error: vi.fn(), info: vi.fn() };
  /** Jak użytkownik zamyka okno: rola przycisku i (dla pola tekstowego) wpisane wartości. */
  let dismissed: { role?: string; data?: unknown } = {};
  let buttons: AlertButton[] = [];
  const alerts = {
    create: vi.fn((options: { buttons: AlertButton[] }) => {
      buttons = options.buttons;
      return Promise.resolve({ present: () => Promise.resolve(), onDidDismiss: () => Promise.resolve(dismissed) });
    }),
  };
  let dialogs: DialogService;

  beforeEach(() => {
    vi.clearAllMocks();
    dismissed = {};
    TestBed.configureTestingModule({
      providers: [
        { provide: AlertController, useValue: alerts },
        { provide: ToastService, useValue: toast },
      ],
    });
    dialogs = TestBed.inject(DialogService);
  });

  it('confirms only when the confirming button was chosen', async () => {
    const question = { header: 'Przyjąć ofertę?', message: '120 zł', confirmText: 'Przyjmij' };

    dismissed = { role: 'cancel' };
    await expect(dialogs.confirm(question)).resolves.toBe(false);

    dismissed = { role: 'confirm' };
    await expect(dialogs.confirm(question)).resolves.toBe(true);
    expect(buttons.map((button) => [button.text, button.role])).toEqual([
      ['Nie', 'cancel'],
      ['Przyjmij', 'confirm'],
    ]);
  });

  it('marks an irreversible action as destructive and names the way back', async () => {
    dismissed = { role: 'destructive' };

    const confirmed = await dialogs.confirm({
      header: 'Usunąć alert?',
      message: '„Dom”',
      confirmText: 'Usuń',
      cancelText: 'Anuluj',
      destructive: true,
    });

    expect(confirmed).toBe(true);
    expect(buttons.map((button) => [button.text, button.role])).toEqual([
      ['Anuluj', 'cancel'],
      ['Usuń', 'destructive'],
    ]);
  });

  const prompt = {
    header: 'Twoje stanowisko',
    message: 'Opisz, jak to wyglądało.',
    placeholder: 'Np. inny zakres…',
    confirmText: 'Wyślij',
    minLength: 2,
    maxLength: 500,
    tooShortMessage: 'Stanowisko: co najmniej 2 znaki.',
  };

  it('returns the trimmed text from the text window', async () => {
    dismissed = { role: 'confirm', data: { values: { text: '  Inny zakres.  ' } } };

    await expect(dialogs.prompt(prompt)).resolves.toBe('Inny zakres.');
  });

  it('keeps the text window open while the text is too short', async () => {
    dismissed = { role: 'cancel' };
    await expect(dialogs.prompt(prompt)).resolves.toBeNull();

    const send = buttons.find((button) => button.role === 'confirm');
    expect(send?.handler?.({ text: ' a ' })).toBe(false);
    expect(toast.error).toHaveBeenCalledWith('Stanowisko: co najmniej 2 znaki.');
    expect(send?.handler?.({ text: 'ok' })).toBe(true);
  });
});
