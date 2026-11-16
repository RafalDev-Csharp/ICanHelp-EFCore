import type { HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, of, switchMap, throwError } from 'rxjs';

import { isApiUrl } from '../api/api.providers';
import { AuthService } from '../auth/auth.service';

/** Logowanie, rejestracja i odświeżanie nie potrzebują tokenu – i nie mogą wywołać odświeżania (pętla). */
const SESSION_PATHS = ['/api/users/sign-in', '/api/users/sign-up', '/api/users/refresh', '/api/users/sign-out'];

/**
 * `Authorization: Bearer …` dla żądań do API. Token, który zaraz wygaśnie, odświeża przed wysłaniem;
 * na 401 odświeża raz i ponawia żądanie. Nieudane odświeżenie = wylogowanie (AuthService), błąd idzie dalej.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApiUrl(req.url) || SESSION_PATHS.some((path) => req.url.includes(path))) {
    return next(req);
  }

  const auth = inject(AuthService);
  if (!auth.isAuthenticated()) {
    return next(req);
  }

  const token$ = auth.isAccessTokenExpiring()
    ? auth.refreshAccessToken().pipe(catchError(() => of(auth.accessToken())))
    : of(auth.accessToken());

  return token$.pipe(
    switchMap((usedToken) =>
      send(req, next, usedToken).pipe(
        catchError((error: unknown) => {
          if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !auth.isAuthenticated()) {
            return throwError(() => error);
          }
          // Inne żądanie już odświeżyło token – wystarczy ponowić z nowym.
          const current = auth.accessToken();
          const retryToken$ = current && current !== usedToken ? of(current) : auth.refreshAccessToken();
          return retryToken$.pipe(
            catchError(() => throwError(() => error)),
            switchMap((token) => send(req, next, token)),
          );
        }),
      ),
    ),
  );
};

function send(req: HttpRequest<unknown>, next: HttpHandlerFn, token: string | null): ReturnType<HttpHandlerFn> {
  return next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
}
