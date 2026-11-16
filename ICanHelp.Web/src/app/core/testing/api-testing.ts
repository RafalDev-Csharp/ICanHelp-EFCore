import type { JwtDto } from '../api/api-client';

/**
 * Wygenerowany klient (NSwag) prosi o odpowiedzi jako `Blob` – `TestRequest.flush` nie zamienia sam obiektu na Blob,
 * więc odpowiedzi w testach podajemy tak.
 */
export function jsonBlob(value: unknown): Blob {
  return new Blob([JSON.stringify(value)], { type: 'application/json' });
}

/** Ciało błędu jak z `DomainExceptionHandler`. */
export function problemBlob(status: number, errorCode?: string): Blob {
  return new Blob([JSON.stringify({ status, title: 'Error', errorCode, traceId: 'test' })], {
    type: 'application/problem+json',
  });
}

/** Para tokenów; dostęp ważny `minutes` minut od teraz. */
export function jwt(name: string, minutes = 60): JwtDto {
  const now = Date.now();
  return {
    accessToken: `access-${name}`,
    expiresAtUtc: new Date(now + minutes * 60_000).toISOString(),
    refreshToken: `refresh-${name}`,
    refreshTokenExpiresAtUtc: new Date(now + 30 * 24 * 3_600_000).toISOString(),
  };
}
