import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AlertController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import { AlertsClient, CategoriesClient } from '../../core/api/api-client';
import { ToastService } from '../../core/ui/toast.service';

import { AlertsPage } from './alerts.page';
import { ALERT_CATEGORIES, alertFixture } from './testing/alert-fixtures';

describe('AlertsPage', () => {
  const client = { getMine: vi.fn(), enable: vi.fn(), disable: vi.fn(), delete: vi.fn() };
  const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };
  let dismissRole = 'destructive';
  const dialogs = {
    create: vi.fn(() =>
      Promise.resolve({
        present: () => Promise.resolve(),
        onDidDismiss: () => Promise.resolve({ role: dismissRole }),
      }),
    ),
  };

  function create(): { page: AlertsPage; element: HTMLElement; render: () => void } {
    TestBed.configureTestingModule({
      imports: [AlertsPage],
      providers: [
        provideRouter([]),
        { provide: AlertsClient, useValue: client },
        { provide: CategoriesClient, useValue: { getAll: () => of(ALERT_CATEGORIES) } },
        { provide: ToastService, useValue: toast },
        { provide: AlertController, useValue: dialogs },
      ],
    });
    const fixture = TestBed.createComponent(AlertsPage);
    fixture.componentInstance.ionViewWillEnter();
    fixture.detectChanges();
    return {
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: () => fixture.detectChanges(),
    };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    dismissRole = 'destructive';
  });

  it('lists the alerts with their kind, category, radius and schedule', () => {
    client.getMine.mockReturnValue(
      of([
        alertFixture(),
        alertFixture({
          id: 'al-2',
          name: 'Droga z pracy',
          kind: 'Route',
          radiusKm: 2,
          subcategoryIds: ['sub-plumbing'],
          schedule: { days: 'Workdays', from: '15:00:00', to: '18:00:00' },
          isEnabled: false,
        }),
      ]),
    );
    const { element } = create();

    const items = element.querySelectorAll('[data-testid="alert-item"]');
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('W okolicy · Naprawy · 5 km · Codziennie, całą dobę');
    expect(items[1].textContent).toContain('Po drodze · Naprawy (1) · 2 km od trasy · Dni robocze, 15:00–18:00');
    expect(element.querySelector('[data-testid="add-area"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="add-route"]')).not.toBeNull();
  });

  it('shows the empty state with both add buttons', () => {
    client.getMine.mockReturnValue(of([]));
    const { element } = create();

    expect(element.textContent).toContain('Nie masz jeszcze alertów');
    expect(element.querySelector('[data-testid="add-route"]')).not.toBeNull();
  });

  it('switches an alert off at once and back on', () => {
    client.getMine.mockReturnValue(of([alertFixture()]));
    client.disable.mockReturnValue(of(undefined));
    client.enable.mockReturnValue(of(undefined));
    const { page } = create();

    page.setEnabled(page.items()[0], false);
    expect(client.disable).toHaveBeenCalledWith('al-1');
    expect(page.items()[0].isEnabled).toBe(false);

    page.setEnabled(page.items()[0], true);
    expect(client.enable).toHaveBeenCalledWith('al-1');
    expect(page.items()[0].isEnabled).toBe(true);
    expect(page.busy().size).toBe(0);
  });

  it('restores the toggle when the API refuses', () => {
    client.getMine.mockReturnValue(of([alertFixture()]));
    client.disable.mockReturnValue(throwError(() => new Error('500')));
    const { page } = create();

    page.setEnabled(page.items()[0], false);

    expect(page.items()[0].isEnabled).toBe(true);
    expect(page.busy().size).toBe(0);
  });

  it('deletes after confirmation only', async () => {
    client.getMine.mockReturnValue(of([alertFixture()]));
    client.delete.mockReturnValue(of(undefined));
    const { page } = create();

    dismissRole = 'cancel';
    await page.remove(page.items()[0]);
    expect(client.delete).not.toHaveBeenCalled();

    dismissRole = 'destructive';
    await page.remove(page.items()[0]);
    expect(client.delete).toHaveBeenCalledWith('al-1');
    expect(page.items()).toHaveLength(0);
    expect(toast.success).toHaveBeenCalledWith('Alert usunięty.');
  });

  it('hides the add buttons at the limit of 10 alerts', () => {
    client.getMine.mockReturnValue(of(Array.from({ length: 10 }, (_, i) => alertFixture({ id: `al-${i}` }))));
    const { element } = create();

    expect(element.querySelector('[data-testid="alert-limit"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="add-area"]')).toBeNull();
  });

  it('offers a retry when the list fails', () => {
    client.getMine.mockReturnValue(throwError(() => new Error('offline')));
    const { page, element } = create();

    expect(page.state()).toBe('failed');
    expect(element.textContent).toContain('Nie udało się wczytać alertów');
  });
});
