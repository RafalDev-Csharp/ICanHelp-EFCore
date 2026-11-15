import { TestBed } from '@angular/core/testing';

import { LoadFailedComponent } from './load-failed.component';
import { LoadingComponent } from './loading.component';

describe('load state components', () => {
  it('marks the loading area as busy', () => {
    const fixture = TestBed.createComponent(LoadingComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[aria-busy="true"] ion-spinner')).not.toBeNull();
  });

  it('shows what failed and asks to retry', () => {
    const fixture = TestBed.createComponent(LoadFailedComponent);
    fixture.componentRef.setInput('heading', 'Nie udało się wczytać rozmów');
    const retried = vi.fn();
    fixture.componentInstance.retry.subscribe(retried);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('app-empty-state h2')?.textContent).toContain('Nie udało się wczytać rozmów');
    expect(element.textContent).toContain('Sprawdź połączenie z internetem');

    element.querySelector('ion-button')?.dispatchEvent(new Event('click'));

    expect(retried).toHaveBeenCalledTimes(1);
  });
});
