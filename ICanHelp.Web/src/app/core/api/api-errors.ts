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
