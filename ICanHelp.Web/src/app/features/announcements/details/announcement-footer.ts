/**
 * Wersja stopki szczegółów ogłoszenia (przyciski zależą od roli czytającego i statusu):
 * - `interrupted` – uczestnik po przerwaniu: notka o rozliczeniu, stanowisko, u właściciela „Wystaw ponownie”;
 * - `completed` – uczestnik po zakończeniu: ocena drugiej strony;
 * - `job` – uczestnik przyjętego zlecenia (Accepted / InProgress): krok realizacji, rozmowa, wyjście;
 * - `owner` – właściciel poza zleceniem: „Edytuj” / „Anuluj” (Open) albo sama notka;
 * - `abandoned` – pomocnik po zerwanym zleceniu: nie złoży już oferty;
 * - `open` – gość albo pomocnik: „Zapytaj” i „Złóż ofertę”;
 * - `closed` – ogłoszenie nie przyjmuje już ofert.
 */
export type FooterMode = 'interrupted' | 'completed' | 'job' | 'owner' | 'abandoned' | 'open' | 'closed';

export interface FooterContext {
  status: string;
  isOwner: boolean;
  /** Właściciel albo pomocnik, którego ofertę przyjęto. */
  isParticipant: boolean;
  /** Zlecenie przerwano (jest wpis o przerwaniu). */
  interrupted: boolean;
  /** Czytający to pomocnik, którego zlecenie zerwano przed rozpoczęciem. */
  abandoned: boolean;
}

export function footerMode(context: FooterContext): FooterMode {
  const { status } = context;
  if (context.isParticipant) {
    if (context.interrupted) {
      return 'interrupted';
    }
    if (status === 'Completed') {
      return 'completed';
    }
    if (status === 'Accepted' || status === 'InProgress') {
      return 'job';
    }
  }
  if (context.isOwner) {
    return 'owner';
  }
  if (context.abandoned) {
    return 'abandoned';
  }
  return status === 'Open' ? 'open' : 'closed';
}
