/** Pierwsza litera wielka: „brak kontaktu” → „Brak kontaktu”. */
export function capitalize(text: string): string {
  return text ? text[0].toUpperCase() + text.slice(1) : text;
}
