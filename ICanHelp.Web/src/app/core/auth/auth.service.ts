import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import type { Observable } from 'rxjs';
import { EMPTY, catchError, finalize, map, of, shareReplay, switchMap, tap, throwError } from 'rxjs';

import { parseApiDate } from '../../shared/pipes/time-ago.pipe';
import type { JwtDto, SignUp, UserDto } from '../api/api-client';
import { UsersClient } from '../api/api-client';
import { errorStatus } from '../api/api-errors';

import type { AuthSession } from './auth-storage';
import { AUTH_STORAGE_KEY, readSession, writeSession } from './auth-storage';

/** Token dostępu odświeżamy z wyprzedzeniem – żeby nie wygasł w drodze do serwera. */
const EXPIRY_MARGIN_MS = 30_000;

/**
 * Sesja użytkownika: tokeny (sygnał + `localStorage`), zalogowany użytkownik (`currentUser`), logowanie,
 * rejestracja, odświeżanie tokenu (jedno naraz, także między kartami) i wylogowanie.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly users = inject(UsersClient);
  private readonly session = signal<AuthSession | null>(readSession());
  private refreshInFlight: Observable<string> | null = null;

  /** Dane zalogowanego (`GET api/users/me`); `null` – niezalogowany albo jeszcze nie wczytany. */
  readonly currentUser = signal<UserDto | null>(null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  constructor() {
    // Inna karta zalogowała / odświeżyła / wylogowała – przejmujemy jej stan.
    const onStorage = (event: StorageEvent): void => {
      if (event.key === AUTH_STORAGE_KEY || event.key === null) {
        this.session.set(readSession());
        if (!this.session()) {
          this.currentUser.set(null);
        }
      }
    };
    window.addEventListener('storage', onStorage);
    inject(DestroyRef).onDestroy(() => window.removeEventListener('storage', onStorage));
  }

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  /** Token dostępu wygasł albo zaraz wygaśnie – przed wysłaniem żądania trzeba go odświeżyć. */
  isAccessTokenExpiring(now: number = Date.now()): boolean {
    const session = this.session();
    return session !== null && isExpiring(session, now);
  }

  signIn(email: string, password: string): Observable<UserDto> {
    return this.users.login({ email, password }).pipe(
      tap((jwt) => this.storeSession(jwt)),
      switchMap(() => this.loadCurrentUser()),
    );
  }

  /** Rejestracja, a po niej od razu logowanie (API nie loguje samo). */
  signUp(data: SignUp): Observable<UserDto> {
    return this.users.register(data).pipe(switchMap(() => this.signIn(data.email, data.password)));
  }

  /** Wczytuje `currentUser`. Niezalogowany – `null` bez wołania API. */
  loadCurrentUser(): Observable<UserDto> {
    if (!this.isAuthenticated()) {
      this.currentUser.set(null);
      return EMPTY;
    }
    return this.users.me().pipe(tap((user) => this.currentUser.set(user)));
  }

  /**
   * Nowy token dostępu za refresh token. Równoczesne wywołania dostają ten sam wynik (jedno żądanie),
   * a jeśli inna karta już odświeżyła – bierzemy jej token z `localStorage` (stary jest jednorazowy).
   * Odrzucony refresh token (401/403/404) = koniec sesji.
   */
  refreshAccessToken(): Observable<string> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    const stored = readSession();
    const current = this.session();
    if (stored && stored.refreshToken !== current?.refreshToken && !isExpiring(stored, Date.now())) {
      this.session.set(stored);
      return of(stored.accessToken);
    }

    const refreshToken = (stored ?? current)?.refreshToken;
    if (!refreshToken) {
      this.clearSession();
      return throwError(() => new Error('Brak sesji do odświeżenia.'));
    }

    this.refreshInFlight = this.users.refresh({ refreshToken }).pipe(
      map((jwt) => {
        this.storeSession(jwt);
        return jwt.accessToken;
      }),
      catchError((error: unknown) => {
        const latest = readSession();
        if (latest && latest.refreshToken !== refreshToken) {
          // Wyścig z inną kartą – ona wygrała i zapisała nowy token.
          this.session.set(latest);
          return of(latest.accessToken);
        }
        const status = errorStatus(error);
        if (status === 401 || status === 403 || status === 404) {
          this.clearSession();
        }
        return throwError(() => error);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshInFlight;
  }

  /** Wylogowanie: lokalnie zawsze, a refresh token unieważniamy w API „przy okazji” (błąd sieci nie blokuje). */
  signOut(): Observable<void> {
    const refreshToken = this.session()?.refreshToken;
    this.clearSession();
    if (!refreshToken) {
      return of(undefined);
    }
    return this.users.logout({ refreshToken }).pipe(catchError(() => of(undefined)));
  }

  /** Czyści sesję bez wołania API (np. odrzucony refresh token). */
  clearSession(): void {
    this.session.set(null);
    this.currentUser.set(null);
    writeSession(null);
  }

  private storeSession(jwt: JwtDto): void {
    const session: AuthSession = {
      accessToken: jwt.accessToken,
      expiresAtUtc: jwt.expiresAtUtc,
      refreshToken: jwt.refreshToken,
      refreshTokenExpiresAtUtc: jwt.refreshTokenExpiresAtUtc,
    };
    this.session.set(session);
    writeSession(session);
  }
}

function isExpiring(session: AuthSession, now: number): boolean {
  return parseApiDate(session.expiresAtUtc).getTime() - EXPIRY_MARGIN_MS <= now;
}
