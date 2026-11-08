import type { HttpInterceptorFn } from '@angular/common/http';
import { HttpContextToken, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { isApiUrl } from '../api/api.providers';
import { ToastService } from '../ui/toast.service';

import { errorMessage } from './error-messages';
import { readProblem } from './problem-details';

/** Żądanie, którego błąd ekran obsługuje sam (bez toastu), np. `new HttpContext().set(SKIP_ERROR_TOAST, true)`. */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/**
 * Żądania sesji: o błędach odświeżenia/wylogowania mówi błąd żądania, które je wywołało (bez podwójnego toastu),
 * a wylogowanie ma się udać zawsze.
 */
const SILENT_PATHS = ['/api/users/refresh', '/api/users/sign-out'];

/**
 * Jedno miejsce na błędy API: ProblemDetails → toast po polsku (`errorCode` → tekst, inaczej komunikat dla kodu HTTP).
 * Błąd idzie dalej, więc ekran może jeszcze np. zatrzymać spinner.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      const silent =
        !(error instanceof HttpErrorResponse) ||
        !isApiUrl(req.url) ||
        req.context.get(SKIP_ERROR_TOAST) ||
        SILENT_PATHS.some((path) => req.url.includes(path));
      if (silent) {
        return throwError(() => error);
      }

      return from(readProblem(error)).pipe(
        switchMap((problem) => {
          void toast.error(errorMessage(error.status, problem?.errorCode));
          return throwError(() => error);
        }),
      );
    }),
  );
};
