import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { readSession, writeSession } from '../auth/auth-storage';
import { AuthService } from '../auth/auth.service';
import { jsonBlob, jwt, problemBlob } from '../testing/api-testing';

import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;

  function setup(): void {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => http.verify());

  it('sends requests without a token when nobody is signed in', async () => {
    setup();

    const result = firstValueFrom(client.get('/api/announcements'));
    const req = http.expectOne('/api/announcements');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
    await result;
  });

  it('adds the bearer token to API requests only', async () => {
    writeSession(jwt('a'));
    setup();

    const api = firstValueFrom(client.get('/api/users/me'));
    const external = firstValueFrom(client.get('https://nominatim.openstreetmap.org/search'));
    const apiReq = http.expectOne('/api/users/me');
    const externalReq = http.expectOne('https://nominatim.openstreetmap.org/search');
    expect(apiReq.request.headers.get('Authorization')).toBe('Bearer access-a');
    expect(externalReq.request.headers.has('Authorization')).toBe(false);
    apiReq.flush({});
    externalReq.flush([]);
    await Promise.all([api, external]);
  });

  it('does not add the token to session endpoints', async () => {
    writeSession(jwt('a'));
    setup();

    const result = firstValueFrom(client.post('/api/users/sign-in', {}));
    const req = http.expectOne('/api/users/sign-in');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
    await result;
  });

  it('on 401 refreshes the token once and repeats the request', async () => {
    writeSession(jwt('old'));
    setup();

    const result = firstValueFrom(client.get('/api/users/me'));
    http.expectOne('/api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    (await vi.waitFor(() => http.expectOne('/api/users/refresh'))).flush(jsonBlob(jwt('new')));
    const retry = await vi.waitFor(() => http.expectOne('/api/users/me'));
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-new');
    retry.flush({ id: 'me' });

    expect(await result).toEqual({ id: 'me' });
  });

  it('refreshes before sending when the token is about to expire', async () => {
    writeSession(jwt('expiring', 0.1));
    setup();

    const result = firstValueFrom(client.get('/api/users/me'));
    http.expectOne('/api/users/refresh').flush(jsonBlob(jwt('fresh')));
    const req = await vi.waitFor(() => http.expectOne('/api/users/me'));
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-fresh');
    req.flush({});
    await result;
  });

  it('passes the 401 on and signs out when the refresh token is rejected', async () => {
    writeSession(jwt('old'));
    setup();

    const result = firstValueFrom(client.get('/api/users/me'));
    http.expectOne('/api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    (await vi.waitFor(() => http.expectOne('/api/users/refresh'))).flush(problemBlob(401, 'InvalidRefreshToken'), {
      status: 401,
      statusText: 'Unauthorized',
    });

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
    expect(readSession()).toBeNull();
  });

  it('does not refresh when the 401 comes for an anonymous request', async () => {
    setup();

    const result = firstValueFrom(client.get('/api/me/conversations'));
    http.expectOne('/api/me/conversations').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(result).rejects.toMatchObject({ status: 401 });
    http.expectNone('/api/users/refresh');
  });
});
