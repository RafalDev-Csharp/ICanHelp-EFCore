import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';

import { RegisterPage } from './register.page';

describe('RegisterPage', () => {
  const auth = { signUp: vi.fn(), isAuthenticated: signal(false) };
  const valid = {
    userName: 'jan.kowalski',
    fullName: 'Jan Kowalski',
    email: 'jan@example.com',
    password: 'Haslo123!',
    phoneNumber: '+48600100200',
    country: 'Polska',
    city: 'Augustów',
    postalCode: '16-300',
    description: '',
  };

  function create(): RegisterPage {
    TestBed.configureTestingModule({
      imports: [RegisterPage],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    const fixture = TestBed.createComponent(RegisterPage);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  beforeEach(() => auth.signUp.mockReset());

  it.each([
    ['userName', 'ab'],
    ['userName', 'x'.repeat(31)],
    ['fullName', '   '],
    ['email', 'jan@'],
    ['password', '12345'],
    ['phoneNumber', '600-100'],
    ['phoneNumber', '+0600100200'],
    ['city', ''],
    ['description', 'a'],
  ] as const)('rejects %s = "%s" like the backend does', (field, value) => {
    const page = create();
    page.form.setValue({ ...valid, [field]: value });

    page.submit();

    expect(page.form.controls[field].invalid).toBe(true);
    expect(auth.signUp).not.toHaveBeenCalled();
  });

  it('sends trimmed data without an empty description and continues to the profile', () => {
    auth.signUp.mockReturnValue(of({}));
    const page = create();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    page.form.setValue({ ...valid, fullName: ' Jan Kowalski ', description: '  ' });

    page.submit();

    expect(auth.signUp).toHaveBeenCalledWith({ ...valid, description: undefined });
    expect(navigate).toHaveBeenCalledWith('/profile', { replaceUrl: true });
  });

  it('removes spaces and dashes from the phone number', () => {
    const page = create();
    page.form.controls.phoneNumber.setValue('+48 600-100-200');

    page.normalizePhone();

    expect(page.form.controls.phoneNumber.value).toBe('+48600100200');
    expect(page.form.controls.phoneNumber.valid).toBe(true);
  });
});
