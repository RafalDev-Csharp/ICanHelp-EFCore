import type { HttpErrorResponse } from '@angular/common/http';

/** Ciało błędu z API (RFC 9457) + nasze rozszerzenia z `DomainExceptionHandler`. */
export interface ApiProblem {
  status?: number;
  title?: string;
  detail?: string;
  errorCode?: string;
  traceId?: string;
}

/**
 * Wygenerowany klient (NSwag) prosi o odpowiedź jako `Blob`, więc ciało błędu bywa Blobem, tekstem albo obiektem.
 * Nie-JSON (np. strona proxy) → `null`.
 */
export async function readProblem(error: HttpErrorResponse): Promise<ApiProblem | null> {
  try {
    const body: unknown = error.error;
    if (body instanceof Blob) {
      return parse(await body.text());
    }
    if (typeof body === 'string') {
      return parse(body);
    }
    return typeof body === 'object' && body !== null ? (body as ApiProblem) : null;
  } catch {
    return null;
  }
}

function parse(text: string): ApiProblem | null {
  if (!text) {
    return null;
  }
  const value: unknown = JSON.parse(text);
  return typeof value === 'object' && value !== null ? (value as ApiProblem) : null;
}
