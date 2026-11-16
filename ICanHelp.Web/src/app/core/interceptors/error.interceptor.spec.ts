import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { problemBlob } from '../testing/api-testing';
import { ToastService } from '../ui/toast.service';

import { ERROR_MESSAGES, errorMessage } from './error-messages';
import { SKIP_ERROR_TOAST, errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  const toast = { error: vi.fn(() => Promise.resolve()) };

  beforeEach(() => {
    toast.error.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toast },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
  });

  afterEach(() => http.verify());

  async function fail(url: string, status: number, body: Blob | null, context?: HttpContext): Promise<void> {
    const result = firstValueFrom(client.get(url, { responseType: 'blob', context }));
    http.expectOne(url).flush(body, { status, statusText: 'Error' });
    await expect(result).rejects.toMatchObject({ status });
  }

  it('translates errorCode from ProblemDetails (Blob body from the NSwag client)', async () => {
    await fail('/api/users/sign-in', 401, problemBlob(401, 'InvalidCredentials'));

    expect(toast.error).toHaveBeenCalledWith('Nieprawidłowy e-mail lub hasło.');
  });

  it('falls back to a message for the HTTP status when there is no errorCode', async () => {
    await fail('/api/announcements', 400, new Blob(['{"title":"One or more validation errors occurred."}']));

    expect(toast.error).toHaveBeenCalledWith('Niepoprawne dane – sprawdź formularz.');
  });

  it('says there is no connection when the request did not reach the server', async () => {
    const result = firstValueFrom(client.get('/api/announcements'));
    http.expectOne('/api/announcements').error(new ProgressEvent('error'));

    await expect(result).rejects.toMatchObject({ status: 0 });
    expect(toast.error).toHaveBeenCalledWith('Brak połączenia z serwerem – sprawdź internet.');
  });

  it('stays silent for refresh, for requests that opt out and for other domains', async () => {
    await fail('/api/users/refresh', 401, problemBlob(401, 'InvalidRefreshToken'));
    await fail('/api/announcements', 409, problemBlob(409), new HttpContext().set(SKIP_ERROR_TOAST, true));
    await fail('https://nominatim.openstreetmap.org/search', 429, null);

    expect(toast.error).not.toHaveBeenCalled();
  });
});

describe('errorMessage', () => {
  it('uses the Polish text for a known errorCode', () => {
    expect(errorMessage(409, 'ConcurrencyConflict')).toBe(ERROR_MESSAGES['ConcurrencyConflict']);
  });

  it('uses the status message for an unknown errorCode', () => {
    expect(errorMessage(404, 'SomethingNew')).toBe('Nie znaleziono.');
    expect(errorMessage(503, undefined)).toBe('Błąd serwera – spróbuj ponownie za chwilę.');
  });
});
