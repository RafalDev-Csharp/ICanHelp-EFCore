import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { jsonBlob, jwt, problemBlob } from '../testing/api-testing';

import { AUTH_STORAGE_KEY, readSession, writeSession } from './auth-storage';
import { AuthService } from './auth.service';

const user = {
  id: '6f2c1a52-1111-4a5b-9c3d-000000000001',
  userName: 'jan.kowalski',
  fullName: 'Jan Kowalski',
  role: 'User',
  createdAt: '2026-11-01T10:00:00',
  isLocked: false,
  description: undefined,
  contactData: {
    email: 'jan@example.com',
    phoneNumber: '+48600100200',
    country: 'Polska',
    city: 'Augustów',
    postalCode: '16-300',
  },
};

describe('AuthService', () => {
  let http: HttpTestingController;

  function create(): AuthService {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => http.verify());

  it('signs in: stores the tokens and loads the current user', async () => {
    const auth = create();

    const result = firstValueFrom(auth.signIn('jan@example.com', 'Haslo123!'));
    const signIn = http.expectOne('/api/users/sign-in');
    expect(JSON.parse(signIn.request.body as string)).toEqual({ email: 'jan@example.com', password: 'Haslo123!' });
    signIn.flush(jsonBlob(jwt('a')));
    (await vi.waitFor(() => http.expectOne('/api/users/me'))).flush(jsonBlob(user));

    expect((await result).userName).toBe('jan.kowalski');
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken()).toBe('access-a');
    expect(auth.currentUser()?.fullName).toBe('Jan Kowalski');
    expect(readSession()?.refreshToken).toBe('refresh-a');
  });

  it('restores the session saved by an earlier visit', () => {
    writeSession(jwt('saved'));

    const auth = create();

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken()).toBe('access-saved');
  });

  it('knows when the access token is about to expire', () => {
    writeSession(jwt('short', 0.25));

    expect(create().isAccessTokenExpiring()).toBe(true);
  });

  it('refreshes once for simultaneous callers and rotates the tokens', async () => {
    writeSession(jwt('old'));
    const auth = create();

    const first = firstValueFrom(auth.refreshAccessToken());
    const second = firstValueFrom(auth.refreshAccessToken());
    const refresh = http.expectOne('/api/users/refresh');
    expect(JSON.parse(refresh.request.body as string)).toEqual({ refreshToken: 'refresh-old' });
    refresh.flush(jsonBlob(jwt('new')));

    expect(await first).toBe('access-new');
    expect(await second).toBe('access-new');
    expect(readSession()?.refreshToken).toBe('refresh-new');
  });

  it('ends the session when the refresh token is rejected', async () => {
    writeSession(jwt('old'));
    const auth = create();

    const result = firstValueFrom(auth.refreshAccessToken());
    http
      .expectOne('/api/users/refresh')
      .flush(problemBlob(401, 'InvalidRefreshToken'), { status: 401, statusText: 'Unauthorized' });

    await expect(result).rejects.toBeTruthy();
    expect(auth.isAuthenticated()).toBe(false);
    expect(readSession()).toBeNull();
  });

  it('keeps the session when the refresh fails because of the network', async () => {
    writeSession(jwt('old'));
    const auth = create();

    const result = firstValueFrom(auth.refreshAccessToken());
    http.expectOne('/api/users/refresh').error(new ProgressEvent('error'));

    await expect(result).rejects.toBeTruthy();
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('takes the token another tab has already refreshed instead of using the old one again', async () => {
    writeSession(jwt('old'));
    const auth = create();
    writeSession(jwt('from-other-tab'));

    expect(await firstValueFrom(auth.refreshAccessToken())).toBe('access-from-other-tab');
    http.expectNone('/api/users/refresh');
  });

  it('follows sign-out in another tab', () => {
    writeSession(jwt('a'));
    const auth = create();

    localStorage.removeItem(AUTH_STORAGE_KEY);
    window.dispatchEvent(new StorageEvent('storage', { key: AUTH_STORAGE_KEY }));

    expect(auth.isAuthenticated()).toBe(false);
  });

  it('signs out locally at once and revokes the refresh token in the API', async () => {
    writeSession(jwt('a'));
    const auth = create();

    const result = firstValueFrom(auth.signOut(), { defaultValue: undefined });
    expect(auth.isAuthenticated()).toBe(false);
    expect(readSession()).toBeNull();

    const signOut = http.expectOne('/api/users/sign-out');
    expect(JSON.parse(signOut.request.body as string)).toEqual({ refreshToken: 'refresh-a' });
    signOut.flush(null, { status: 204, statusText: 'No Content' });
    await result;
  });

  it('signs up and then signs in with the same e-mail and password', async () => {
    const auth = create();
    const data = {
      userName: 'jan.kowalski',
      fullName: 'Jan Kowalski',
      email: 'jan@example.com',
      password: 'Haslo123!',
      phoneNumber: '+48600100200',
      country: 'Polska',
      city: 'Augustów',
      postalCode: '16-300',
      description: undefined,
    };

    const result = firstValueFrom(auth.signUp(data));
    http.expectOne('/api/users/sign-up').flush(null, { status: 201, statusText: 'Created' });
    const signIn = await vi.waitFor(() => http.expectOne('/api/users/sign-in'));
    expect(JSON.parse(signIn.request.body as string)).toEqual({ email: data.email, password: data.password });
    signIn.flush(jsonBlob(jwt('a')));
    (await vi.waitFor(() => http.expectOne('/api/users/me'))).flush(jsonBlob(user));

    expect((await result).id).toBe(user.id);
  });
});
