import { inject } from '@angular/core';
import type { CanActivateFn, UrlTree } from '@angular/router';
import { Router } from '@angular/router';

import { AuthService } from './auth.service';

/** Trasy wymagające logowania: niezalogowany trafia na `/login?returnUrl=…`, a po zalogowaniu wraca. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isAuthenticated() || loginUrlTree(inject(Router), state.url);

/** Adres ekranu logowania z powrotem do `returnUrl` – także dla akcji („Złóż ofertę” → logowanie → powrót). */
export function loginUrlTree(router: Router, returnUrl: string): UrlTree {
  return router.createUrlTree(['/login'], { queryParams: { returnUrl } });
}

/** Tylko adresy wewnątrz aplikacji (`/coś`, nie `//obca-domena` ani `https://…`) – inaczej domyślny. */
export function safeReturnUrl(returnUrl: string | null | undefined, fallback = '/profile'): string {
  return returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : fallback;
}
