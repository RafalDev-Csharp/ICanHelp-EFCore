import { Component, input, output } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';

import type { GeoPoint } from '../../core/geo/geo-point';
import type { PlannedRoute } from '../../core/geo/routing.service';
import { RoutingService } from '../../core/geo/routing.service';
import { ToastService } from '../../core/ui/toast.service';
import { MapComponent } from '../../shared/components/map/map.component';
import { PlaceSearchComponent } from '../../shared/components/place-search/place-search.component';

import { RoutePickerComponent } from './route-picker.component';

@Component({ selector: 'app-map', template: '' })
class MapStubComponent {
  readonly markers = input<unknown>();
  readonly route = input<unknown>();
  readonly mapClick = output<GeoPoint>();
}

@Component({ selector: 'app-place-search', template: '' })
class PlaceSearchStubComponent {
  readonly label = input<string>();
  readonly placeholder = input<string>();
  readonly picked = output<GeoPoint>();
}

describe('RoutePickerComponent', () => {
  const start: GeoPoint = { latitude: 54.1, longitude: 22.93 };
  const end: GeoPoint = { latitude: 53.844, longitude: 22.979 };
  const road: GeoPoint[] = [start, { latitude: 53.97, longitude: 22.95 }, end];
  const routing = { configured: true, plan: vi.fn() };
  const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };

  function create(): { picker: RoutePickerComponent; element: HTMLElement; render: () => void } {
    TestBed.configureTestingModule({
      imports: [RoutePickerComponent],
      providers: [
        { provide: RoutingService, useValue: routing },
        { provide: ToastService, useValue: toast },
      ],
    });
    TestBed.overrideComponent(RoutePickerComponent, {
      remove: { imports: [MapComponent, PlaceSearchComponent] },
      add: { imports: [MapStubComponent, PlaceSearchStubComponent] },
    });
    const fixture = TestBed.createComponent(RoutePickerComponent);
    fixture.detectChanges();
    return {
      picker: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: () => fixture.detectChanges(),
    };
  }

  beforeEach(() => vi.clearAllMocks());

  it('picks the start, switches to the destination and plans the route', () => {
    routing.plan.mockReturnValue(of<PlannedRoute>({ points: road, followsRoads: true }));
    const { picker, element, render } = create();

    picker.choose({ latitude: 54.10012, longitude: 22.93041 });
    expect(picker.start()).toEqual(start);
    expect(routing.plan).not.toHaveBeenCalled();

    picker.choose(end);
    render();

    expect(picker.end()).toEqual(end);
    expect(routing.plan).toHaveBeenCalledWith(start, end);
    expect(picker.points()).toEqual(road);
    expect(element.querySelector('[data-testid="route-ready"]')?.textContent).toContain('Trasa gotowa');
  });

  it('drops the old route when an end point changes', () => {
    const pending = new Subject<PlannedRoute>();
    routing.plan.mockReturnValue(pending);
    const { picker } = create();
    picker.start.set(start);
    picker.end.set(end);
    picker.points.set(road);

    picker.setActive('end');
    picker.choose({ latitude: 53.9, longitude: 23.1 });

    expect(picker.points()).toEqual([]);
    pending.next({ points: [start, { latitude: 53.9, longitude: 23.1 }], followsRoads: true });
    expect(picker.points()).toHaveLength(2);
  });

  it('marks a straight line when there is no routing key', () => {
    routing.plan.mockReturnValue(of<PlannedRoute>({ points: [start, end], followsRoads: false }));
    const { picker, element, render } = create();
    picker.start.set(start);
    picker.setActive('end');

    picker.choose(end);
    render();

    expect(element.querySelector('[data-testid="route-ready"]')?.textContent).toContain('Linia prosta');
  });

  it('tells about a routing failure and lets the user retry', () => {
    routing.plan.mockReturnValue(throwError(() => new Error('429')));
    const { picker, element, render } = create();
    picker.start.set(start);
    picker.setActive('end');

    picker.choose(end);
    render();

    expect(toast.error).toHaveBeenCalledWith('Nie udało się wyznaczyć trasy – spróbuj ponownie za chwilę.');
    expect(element.querySelector('[data-testid="route-plan"]')).not.toBeNull();
  });

  it('refuses the same point as start and destination', () => {
    const { picker } = create();
    picker.start.set(start);
    picker.setActive('end');

    picker.choose(start);

    expect(routing.plan).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalled();
  });
});
