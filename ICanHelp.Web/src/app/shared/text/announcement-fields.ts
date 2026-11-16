import { capitalize } from './capitalize';

/** Pola ogłoszenia (`AnnouncementFields` w backendzie) – małą literą, do zdań („zmieniono: budżet, opis”). */
const FIELD_LABELS: Readonly<Record<string, string>> = {
  Category: 'kategoria',
  Difficulty: 'trudność',
  Title: 'tytuł',
  Description: 'opis',
  Budget: 'budżet',
  Location: 'miejsce',
  Images: 'zdjęcia',
};

/** „budżet”, „opis”… – nieznane pole zostaje bez tłumaczenia. */
export function announcementFieldLabel(field: string): string {
  return FIELD_LABELS[field] ?? field;
}

/** „Budżet”, „Opis”… – nagłówek wiersza w podglądzie zmian. */
export function announcementFieldTitle(field: string): string {
  const label = FIELD_LABELS[field];
  return label ? capitalize(label) : field;
}
