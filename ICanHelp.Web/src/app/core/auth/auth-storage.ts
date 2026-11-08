import type { JwtDto } from '../api/api-client';

/** Klucz w `localStorage`. Zdarzenie `storage` z tym kluczem = inna karta zalogowała, odświeżyła albo wylogowała. */
export const AUTH_STORAGE_KEY = 'icanhelp.auth';

/** Tokeny zalogowanego użytkownika (to, co zwraca `sign-in` / `refresh`). */
export type AuthSession = Pick<JwtDto, 'accessToken' | 'expiresAtUtc' | 'refreshToken' | 'refreshTokenExpiresAtUtc'>;

/**
 * `localStorage` bywa niedostępny (tryb prywatny, zablokowane dane strony) – wtedy sesja żyje tylko w pamięci,
 * a aplikacja działa dalej.
 */
export function readSession(): AuthSession | null {
  try {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const session = JSON.parse(raw) as Partial<AuthSession>;
    return session.accessToken && session.refreshToken ? (session as AuthSession) : null;
  } catch {
    return null;
  }
}

export function writeSession(session: AuthSession | null): void {
  try {
    if (session) {
      localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
    } else {
      localStorage.removeItem(AUTH_STORAGE_KEY);
    }
  } catch {
    // Brak dostępu do localStorage – sesja zostaje tylko w pamięci.
  }
}
