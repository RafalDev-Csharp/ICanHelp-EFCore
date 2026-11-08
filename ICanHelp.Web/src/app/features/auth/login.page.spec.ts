import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';

import { LoginPage } from './login.page';

describe('LoginPage', () => {
  const auth = { signIn: vi.fn(), isAuthenticated: signal(false) };

  function create(returnUrl?: string): LoginPage {
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    const fixture = TestBed.createComponent(LoginPage);
    if (returnUrl) {
      fixture.componentRef.setInput('returnUrl', returnUrl);
    }
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  beforeEach(() => auth.signIn.mockReset());

  it('does not call the API while the form is invalid', () => {
    const page = create();
    page.form.setValue({ email: 'to-nie-email', password: '' });

    page.submit();

    expect(auth.signIn).not.toHaveBeenCalled();
    expect(page.form.controls.email.touched).toBe(true);
  });

  it('signs in and goes back to returnUrl', () => {
    auth.signIn.mockReturnValue(of({}));
    const page = create('/announcements/42');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    page.form.setValue({ email: 'jan@example.com', password: 'Haslo123!' });

    page.submit();

    expect(auth.signIn).toHaveBeenCalledWith('jan@example.com', 'Haslo123!');
    expect(navigate).toHaveBeenCalledWith('/announcements/42', { replaceUrl: true });
    expect(page.submitting()).toBe(false);
  });

  it('ignores a returnUrl that leads outside the app', () => {
    auth.signIn.mockReturnValue(of({}));
    const page = create('https://evil.example');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    page.form.setValue({ email: 'jan@example.com', password: 'Haslo123!' });

    page.submit();

    expect(navigate).toHaveBeenCalledWith('/profile', { replaceUrl: true });
  });

  it('stays on the page after a failed sign-in (the interceptor shows the message)', () => {
    auth.signIn.mockReturnValue(throwError(() => ({ status: 401 })));
    const page = create();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl');
    page.form.setValue({ email: 'jan@example.com', password: 'zle' });

    page.submit();

    expect(navigate).not.toHaveBeenCalled();
    expect(page.submitting()).toBe(false);
  });
});
