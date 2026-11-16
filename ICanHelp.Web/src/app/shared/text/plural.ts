/**
 * Polska odmiana po liczbie: `pluralPl(1, 'ocena', 'oceny', 'ocen')` → „ocena”, 3 → „oceny”, 5 / 12 / 0 → „ocen”, 22 → „oceny”.
 * `few` – 2–4 (bez 12–14), `many` – reszta.
 */
export function pluralPl(count: number, one: string, few: string, many: string): string {
  const n = Math.abs(Math.trunc(count));
  if (n === 1) {
    return one;
  }
  const lastDigit = n % 10;
  const lastTwo = n % 100;
  return lastDigit >= 2 && lastDigit <= 4 && (lastTwo < 12 || lastTwo > 14) ? few : many;
}
