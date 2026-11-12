import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NavController } from '@ionic/angular';

import { BrandComponent, HOME_URL } from './brand.component';

describe('BrandComponent', () => {
  it('shows the app name and leads to the home page as a new root', () => {
    TestBed.configureTestingModule({ imports: [BrandComponent], providers: [provideRouter([])] });
    const navigateRoot = vi.spyOn(TestBed.inject(NavController), 'navigateRoot').mockResolvedValue(true);
    const fixture = TestBed.createComponent(BrandComponent);
    fixture.detectChanges();
    const button = (fixture.nativeElement as HTMLElement).querySelector('ion-button');

    expect(button?.getAttribute('aria-label')).toContain('I can help U');
    button?.dispatchEvent(new Event('click'));

    expect(navigateRoot).toHaveBeenCalledWith(HOME_URL, { animated: true, animationDirection: 'back' });
  });
});
