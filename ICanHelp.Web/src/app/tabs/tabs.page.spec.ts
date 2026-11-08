import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { TabsPage } from './tabs.page';

describe('TabsPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TabsPage],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  // Teksty etykiet sprawdza e2e (e2e/app.spec.ts) – w jsdom komponenty Ionic czyszczą treść `ion-label`.
  it('links the five tabs to their routes', () => {
    const fixture = TestBed.createComponent(TabsPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const tabs = Array.from(element.querySelectorAll('ion-tab-button')).map((button) => [
      button.getAttribute('tab'),
      button.getAttribute('href'),
    ]);
    expect(tabs).toEqual([
      ['announcements', '/announcements'],
      ['add', '/add'],
      ['conversations', '/conversations'],
      ['notifications', '/notifications'],
      ['profile', '/profile'],
    ]);
  });
});
