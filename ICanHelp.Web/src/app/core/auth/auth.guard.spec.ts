import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import type { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Router, provideRouter } from '@angular/router';

import { jwt } from '../testing/api-testing';

import { writeSession } from './auth-storage';
import { authGuard, safeReturnUrl } from './auth.guard';

describe('authGuard', () => {
  function run(url: string): unknown {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
    return TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot));
  }

  beforeEach(() => localStorage.clear());

  it('lets a signed-in user in', () => {
    writeSession(jwt('a'));

    expect(run('/conversations')).toBe(true);
  });

  it('sends an anonymous user to the login page with the way back', () => {
    const result = run('/conversations');

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Fconversations');
  });
});

describe('safeReturnUrl', () => {
  it('accepts addresses inside the app', () => {
    expect(safeReturnUrl('/announcements/123?tab=offers')).toBe('/announcements/123?tab=offers');
  });

  it.each([undefined, null, '', 'https://evil.example', '//evil.example', 'javascript:alert(1)'])(
    'replaces %s with the default',
    (url) => {
      expect(safeReturnUrl(url)).toBe('/profile');
    },
  );
});
