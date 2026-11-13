import type { AbandonJob, JobExitDto, JobExitReason } from '../../core/api/api-client';
import { JOB_EXIT_REASON_LABELS } from '../../core/notifications/notification-texts';
import { pluralPl } from '../../shared/text/plural';

/**
 * F11 E: wyjście ze zlecenia. `abandon` – „Zrezygnuj” przed rozpoczęciem (Accepted → ogłoszenie znów Open, bez ocen),
 * `interrupt` – „Przerwij zlecenie” w trakcie (InProgress → Interrupted, obie strony mogą się ocenić).
 */
export type JobExitAction = 'abandon' | 'interrupt';

/**
 * Treść obu komend (`AbandonJob` i `InterruptJob` mają te same pola: `reason`, `note`). NSwag spłaszcza wspólną bazę
 * z backendu (`JobExitCommand`), więc typ jest tutaj.
 */
export type JobExitCommand = AbandonJob;

export interface JobExitReasonOption {
  value: JobExitReason;
  label: string;
}

/** Jak w backendzie (`Announcement.EnsureReason`): „konflikt” tylko przy przerwaniu, „Inny” wymaga opisu. */
export function jobExitReasons(action: JobExitAction): JobExitReasonOption[] {
  const values: JobExitReason[] =
    action === 'abandon'
      ? ['CannotMakeIt', 'TermsDiffer', 'NoContact', 'Other']
      : ['CannotMakeIt', 'TermsDiffer', 'NoContact', 'Disagreement', 'Other'];
  return values.map((value) => ({ value, label: capitalize(JOB_EXIT_REASON_LABELS[value]) }));
}

/** „Nie mogę w tym terminie” – powód z API; nieznany zostaje bez tłumaczenia. */
export function jobExitReasonLabel(reason: string): string {
  return capitalize(JOB_EXIT_REASON_LABELS[reason] ?? reason);
}

/** Powód z opisem: „Brak kontaktu – nie odbiera telefonu”, „Inny” → sam opis. */
export function jobExitReasonText(exit: Pick<JobExitDto, 'reason' | 'note'>): string {
  if (exit.reason === 'Other' && exit.note) {
    return exit.note;
  }
  return exit.note ? `${jobExitReasonLabel(exit.reason)} – ${exit.note}` : jobExitReasonLabel(exit.reason);
}

/**
 * Nagłówek wpisu w historii / wątku z perspektywy czytającego: „Zrezygnowałeś ze zlecenia”, „Pomocnik przerwał
 * zlecenie”… `myUserId` – zalogowany (pusty = widz bez roli).
 */
export function jobExitTitle(exit: Pick<JobExitDto, 'kind' | 'actorId' | 'actorRole'>, myUserId: string): string {
  const abandoned = exit.kind === 'Abandoned';
  if (exit.actorId === myUserId) {
    return abandoned ? 'Zrezygnowałeś ze zlecenia' : 'Przerwałeś zlecenie';
  }
  const who = exit.actorRole === 'Client' ? 'Zleceniodawca' : 'Pomocnik';
  return abandoned ? `${who} zrezygnował ze zlecenia` : `${who} przerwał zlecenie`;
}

/**
 * F11 G: czy `myUserId` może dodać stanowisko – jest drugą stroną (nie tym, kto wyszedł) i jeszcze go nie dodał.
 * Druga strona: zleceniodawca, gdy wyszedł pomocnik; pomocnik z wpisu, gdy wyszedł zleceniodawca.
 */
export function canRespondToExit(
  exit: Pick<JobExitDto, 'actorRole' | 'helperId' | 'response'>,
  myUserId: string,
  ownerId: string,
): boolean {
  if (exit.response || !myUserId) {
    return false;
  }
  return exit.actorRole === 'Client' ? myUserId === exit.helperId : myUserId === ownerId;
}

/** Notka po przerwaniu – aplikacja nie obsługuje pieniędzy. */
export const INTERRUPTED_NOTE = 'Ustalcie między sobą rozliczenie za wykonaną część.';

/** Opis licznika z `UserReliabilityDto`: „1 rezygnacja · 2 przerwane zlecenia”, zero obu → pusty tekst. */
export function reliabilityLabel(counts: { abandonedCount: number; interruptedCount: number } | null): string {
  if (!counts) {
    return '';
  }
  const parts: string[] = [];
  if (counts.abandonedCount > 0) {
    parts.push(`${counts.abandonedCount} ${pluralPl(counts.abandonedCount, 'rezygnacja', 'rezygnacje', 'rezygnacji')}`);
  }
  if (counts.interruptedCount > 0) {
    const noun = pluralPl(counts.interruptedCount, 'przerwane zlecenie', 'przerwane zlecenia', 'przerwanych zleceń');
    parts.push(`${counts.interruptedCount} ${noun}`);
  }
  return parts.join(' · ');
}

function capitalize(text: string): string {
  return text ? text[0].toUpperCase() + text.slice(1) : text;
}
