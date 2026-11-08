import { TestBed } from '@angular/core/testing';

import { EmptyStateComponent } from './empty-state.component';

describe('EmptyStateComponent', () => {
  it('renders heading and message', () => {
    const fixture = TestBed.createComponent(EmptyStateComponent);
    fixture.componentRef.setInput('heading', 'Brak rozmów');
    fixture.componentRef.setInput('message', 'Napisz do kogoś');
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('h2')?.textContent).toBe('Brak rozmów');
    expect(element.querySelector('p')?.textContent).toBe('Napisz do kogoś');
  });

  it('hides the message when not set', () => {
    const fixture = TestBed.createComponent(EmptyStateComponent);
    fixture.componentRef.setInput('heading', 'Pusto');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('p')).toBeNull();
  });
});
