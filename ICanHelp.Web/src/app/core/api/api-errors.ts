/**
 * Kod HTTP błędu z dowolnego źródła: `HttpErrorResponse` (interceptory), `ApiException` albo samo ciało
 * ProblemDetails (tak rzucają błędy wygenerowani klienci NSwag). 0 = brak połączenia.
 */
export function errorStatus(error: unknown): number | undefined {
  if (typeof error === 'object' && error !== null && 'status' in error) {
    const status = (error as { status: unknown }).status;
    return typeof status === 'number' ? status : undefined;
  }
  return undefined;
}

/**
 * `errorCode` z ProblemDetails (np. „ConcurrencyConflict”) – gdy ekran musi zareagować na konkretny błąd
 * (komunikat i tak pokazuje interceptor). Klienci NSwag rzucają samo ciało ProblemDetails.
 */
export function errorCode(error: unknown): string | undefined {
  if (typeof error === 'object' && error !== null && 'errorCode' in error) {
    const code = (error as { errorCode: unknown }).errorCode;
    return typeof code === 'string' ? code : undefined;
  }
  return undefined;
}
