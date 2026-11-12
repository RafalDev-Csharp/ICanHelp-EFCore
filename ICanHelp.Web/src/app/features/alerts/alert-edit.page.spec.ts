import { Component, input, model } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { AlertController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import type { AnnouncementDto } from '../../core/api/api-client';
import { AlertsClient, CategoriesClient } from '../../core/api/api-client';
import type { GeoPoint } from '../../core/geo/geo-point';
import { ToastService } from '../../core/ui/toast.service';
import { LocationPickerComponent } from '../announcements/form/location-picker.component';
import { announcement } from '../announcements/testing/announcement-fixtures';

import { AlertEditPage, PREVIEW_LIMIT } from './alert-edit.page';
import { RoutePickerComponent } from './route-picker.component';
import { ALERT_CATEGORIES, alertFixture } from './testing/alert-fixtures';

@Component({ selector: 'app-location-picker', template: '' })
class LocationPickerStubComponent {
  readonly point = model<GeoPoint | null>(null);
  readonly markerTitle = input<string>();
  readonly radiusKm = input<number>();
  readonly hint = input<string>();
}

@Component({ selector: 'app-route-picker', template: '<span data-testid="route-picker-stub"></span>' })
class RoutePickerStubComponent {
  readonly start = model<GeoPoint | null>(null);
  readonly end = model<GeoPoint | null>(null);
  readonly points = model<GeoPoint[]>([]);
  readonly radiusKm = input<number>();
}

describe('AlertEditPage', () => {
  const client = { getById: vi.fn(), create: vi.fn(), update: vi.fn(), delete: vi.fn(), preview: vi.fn() };
  const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };
  const dialogs = {
    create: vi.fn(() =>
      Promise.resolve({
        present: () => Promise.resolve(),
        onDidDismiss: () => Promise.resolve({ role: 'destructive' }),
      }),
    ),
  };

  function create(params: { alertId?: string; kind?: string }): {
    page: AlertEditPage;
    element: HTMLElement;
    render: () => void;
  } {
    TestBed.configureTestingModule({
      imports: [AlertEditPage],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap(params.alertId ? { alertId: params.alertId } : {}),
              queryParamMap: convertToParamMap(params.kind ? { kind: params.kind } : {}),
            },
          },
        },
        { provide: AlertsClient, useValue: client },
        { provide: CategoriesClient, useValue: { getAll: () => of(ALERT_CATEGORIES) } },
        { provide: ToastService, useValue: toast },
        { provide: AlertController, useValue: dialogs },
      ],
    });
    TestBed.overrideComponent(AlertEditPage, {
      remove: { imports: [LocationPickerComponent, RoutePickerComponent] },
      add: { imports: [LocationPickerStubComponent, RoutePickerStubComponent] },
    });
    const fixture = TestBed.createComponent(AlertEditPage);
    fixture.detectChanges();
    return {
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: () => fixture.detectChanges(),
    };
  }

  beforeEach(() => vi.clearAllMocks());

  it('does not send an incomplete alert and shows what is missing', () => {
    const { page, element, render } = create({ kind: 'Area' });

    page.save();
    render();

    expect(client.create).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith('Uzupełnij zaznaczone pola.');
    expect(element.querySelector('[data-testid="place-error"]')).not.toBeNull();
  });

  it('creates an area alert and goes back to the list', () => {
    client.create.mockReturnValue(of({ id: 'al-9' }));
    const { page } = create({ kind: 'Area' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    page.setName('Dom');
    page.setCategory('cat-home');
    page.setSubcategories(['sub-plumbing']);
    page.setCenter({ latitude: 53.844, longitude: 22.979 });
    page.setRadius(7.5);
    page.setDays(31);
    page.setLimitHours(true);
    page.setFrom('15:00');
    page.setTo('18:00');

    page.save();

    expect(client.create).toHaveBeenCalledWith({
      name: 'Dom',
      kind: 'Area',
      categoryId: 'cat-home',
      subcategoryIds: ['sub-plumbing'],
      center: { latitude: 53.844, longitude: 22.979 },
      radiusKm: 7.5,
      schedule: { days: 'Workdays', from: '15:00:00', to: '18:00:00' },
    });
    expect(navigate).toHaveBeenCalledWith('/profile/alerts', { replaceUrl: true });
    expect(page.saving()).toBe(false);
  });

  it('clears subcategories when the category changes', () => {
    const { page } = create({ kind: 'Area' });
    page.setCategory('cat-home');
    page.setSubcategories(['sub-plumbing']);

    page.setCategory('cat-other');

    expect(page.form().subcategoryIds).toEqual([]);
  });

  it('toggles single days', () => {
    const { page } = create({ kind: 'Area' });
    page.setDays(0);

    page.toggleDay(1);
    page.toggleDay(16);
    page.toggleDay(1);

    expect(page.form().days).toBe(16);
  });

  it('starts a route alert with the route picker and the route radius', () => {
    const { page, element } = create({ kind: 'Route' });

    expect(page.form().kind).toBe('Route');
    expect(page.form().radiusKm).toBe(2);
    expect(element.querySelector('[data-testid="route-picker-stub"]')).not.toBeNull();
    expect(element.querySelector('app-location-picker')).toBeNull();
  });

  it('loads a saved alert with its preview and saves changes without the kind', () => {
    const preview: AnnouncementDto[] = [announcement('a1', { title: 'Cieknący kran', distanceKm: 1.2 })];
    client.getById.mockReturnValue(of(alertFixture()));
    client.preview.mockReturnValue(of(preview));
    client.update.mockReturnValue(of(undefined));
    const { page, element } = create({ alertId: 'al-1' });

    expect(client.getById).toHaveBeenCalledWith('al-1');
    expect(client.preview).toHaveBeenCalledWith('al-1', PREVIEW_LIMIT);
    expect(page.preview()).toEqual(preview);
    expect(element.querySelector('[data-testid="alert-preview"]')?.textContent).toContain('Cieknący kran');
    expect(page.form().name).toBe('Dom');

    page.setRadius(10);
    page.save();

    const [id, body] = client.update.mock.calls[0] as [string, Record<string, unknown>];
    expect(id).toBe('al-1');
    expect(body['radiusKm']).toBe(10);
    expect('kind' in body).toBe(false);
    expect(toast.success).toHaveBeenCalledWith('Zapisano zmiany alertu.');
    expect(client.preview).toHaveBeenCalledTimes(2);
  });

  it('says when the alert does not exist', () => {
    client.getById.mockReturnValue(throwError(() => ({ status: 404 })));
    const { page, element } = create({ alertId: 'missing' });

    expect(page.state()).toBe('missing');
    expect(element.textContent).toContain('Nie ma takiego alertu');
  });

  it('deletes the alert after confirmation', async () => {
    client.getById.mockReturnValue(of(alertFixture()));
    client.preview.mockReturnValue(of([]));
    client.delete.mockReturnValue(of(undefined));
    const { page } = create({ alertId: 'al-1' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    await page.remove();

    expect(client.delete).toHaveBeenCalledWith('al-1');
    expect(navigate).toHaveBeenCalledWith('/profile/alerts', { replaceUrl: true });
  });
});
