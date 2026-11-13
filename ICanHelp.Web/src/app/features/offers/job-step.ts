/** Krok realizacji zlecenia, który może zrobić zalogowany (F11 D – te same reguły co `Announcement.Start/ReportDone/Complete`). */
export type JobAction = 'start' | 'report' | 'complete';

export interface JobStep {
  /** Przycisk akcji albo `null` (nic do zrobienia – tylko notka). */
  action: JobAction | null;
  /** Akcja główna (pełny przycisk) czy awaryjna (obrys) – np. potwierdzenie, zanim pomocnik zgłosił wykonanie. */
  primary: boolean;
  /** Notka nad przyciskiem: na co czekamy / co się stało. */
  note: string | null;
  /** Wyróżniona notka – pomocnik zgłosił wykonanie, zleceniodawca ma potwierdzić. */
  highlight: boolean;
}

const NONE: JobStep = { action: null, primary: false, note: null, highlight: false };

export const JOB_ACTION_LABELS: Readonly<Record<JobAction, string>> = {
  start: 'Rozpocznij zlecenie',
  report: 'Zgłoś wykonanie',
  complete: 'Potwierdź wykonanie',
};

export const JOB_ACTION_ICONS: Readonly<Record<JobAction, string>> = {
  start: 'play-outline',
  report: 'flag-outline',
  complete: 'checkmark-done-outline',
};

/**
 * Co pokazać uczestnikowi przyjętego zlecenia:
 * - pomocnik: Accepted → „Rozpocznij”, InProgress → „Zgłoś wykonanie”, po zgłoszeniu – czeka na potwierdzenie;
 * - zleceniodawca: czeka na pomocnika, ale „Potwierdź wykonanie” ma zawsze (z Accepted i InProgress – zlecenie nie może
 *   utknąć, gdy pomocnik zapomni kliknąć); po zgłoszeniu wykonania – wyróżniona notka i pełny przycisk.
 * Inne statusy (Open, Completed, Cancelled) – nic.
 *
 * @param myRole `Client` (właściciel) albo `Helper` (przyjęty pomocnik) – wołający sprawdza, że to uczestnik
 */
export function jobStep(status: string, myRole: string, doneReportedAt: string | undefined): JobStep {
  const helper = myRole === 'Helper';
  if (status === 'Accepted') {
    return helper
      ? { ...NONE, action: 'start', primary: true }
      : { ...NONE, action: 'complete', note: 'Przyjęte – pomocnik rozpocznie zlecenie.' };
  }
  if (status !== 'InProgress') {
    return NONE;
  }
  if (helper) {
    return doneReportedAt
      ? { ...NONE, note: 'Zgłosiłeś wykonanie – czekasz na potwierdzenie zleceniodawcy.' }
      : { ...NONE, action: 'report', primary: true };
  }
  return doneReportedAt
    ? {
        action: 'complete',
        primary: true,
        note: 'Pomocnik zgłosił wykonanie – sprawdź i potwierdź.',
        highlight: true,
      }
    : { ...NONE, action: 'complete', note: 'W realizacji – pomocnik zgłosi wykonanie.' };
}
