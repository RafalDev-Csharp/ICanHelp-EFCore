import { addIcons } from 'ionicons';
import {
  checkmarkDoneOutline,
  exitOutline,
  flagOutline,
  hourglassOutline,
  playOutline,
  stopCircleOutline,
} from 'ionicons/icons';

import type { JobExitAction } from './job-exit';

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
  /**
   * F11 E: legalne wyjście (każda strona) – „Zrezygnuj” przed rozpoczęciem (Accepted), „Przerwij zlecenie” w trakcie
   * (InProgress); `null` w innych statusach.
   */
  exit: JobExitAction | null;
}

const NONE: JobStep = { action: null, primary: false, note: null, highlight: false, exit: null };

export const JOB_EXIT_LABELS: Readonly<Record<JobExitAction, string>> = {
  abandon: 'Zrezygnuj',
  interrupt: 'Przerwij zlecenie',
};

const JOB_EXIT_ICONS: Readonly<Record<JobExitAction, string>> = {
  abandon: 'exit-outline',
  interrupt: 'stop-circle-outline',
};

const JOB_ACTION_LABELS: Readonly<Record<JobAction, string>> = {
  start: 'Rozpocznij zlecenie',
  report: 'Zgłoś wykonanie',
  complete: 'Potwierdź wykonanie',
};

const JOB_ACTION_ICONS: Readonly<Record<JobAction, string>> = {
  start: 'play-outline',
  report: 'flag-outline',
  complete: 'checkmark-done-outline',
};

/**
 * Co pokazać uczestnikowi przyjętego zlecenia:
 * - pomocnik: Accepted → „Rozpocznij”, InProgress → „Zgłoś wykonanie”, po zgłoszeniu – czeka na potwierdzenie;
 * - zleceniodawca: czeka na pomocnika, ale „Potwierdź wykonanie” ma zawsze (z Accepted i InProgress – zlecenie nie może
 *   utknąć, gdy pomocnik zapomni kliknąć); po zgłoszeniu wykonania – wyróżniona notka i pełny przycisk.
 * Obie strony mają też wyjście (F11 E): Accepted – „Zrezygnuj”, InProgress – „Przerwij zlecenie”.
 * Inne statusy (Open, Completed, Cancelled, Interrupted) – nic.
 *
 * @param myRole `Client` (właściciel) albo `Helper` (przyjęty pomocnik) – wołający sprawdza, że to uczestnik
 */
export function jobStep(status: string, myRole: string, doneReportedAt: string | undefined): JobStep {
  const helper = myRole === 'Helper';
  if (status === 'Accepted') {
    const exit: JobExitAction = 'abandon';
    return helper
      ? { ...NONE, action: 'start', primary: true, exit }
      : { ...NONE, action: 'complete', note: 'Przyjęte – pomocnik rozpocznie zlecenie.', exit };
  }
  if (status !== 'InProgress') {
    return NONE;
  }
  const exit: JobExitAction = 'interrupt';
  if (helper) {
    return doneReportedAt
      ? { ...NONE, note: 'Zgłosiłeś wykonanie – czekasz na potwierdzenie zleceniodawcy.', exit }
      : { ...NONE, action: 'report', primary: true, exit };
  }
  return doneReportedAt
    ? {
        action: 'complete',
        primary: true,
        note: 'Pomocnik zgłosił wykonanie – sprawdź i potwierdź.',
        highlight: true,
        exit,
      }
    : { ...NONE, action: 'complete', note: 'W realizacji – pomocnik zgłosi wykonanie.', exit };
}

/** Krok zlecenia gotowy do narysowania – teksty, ikony i kolory w jednym miejscu (wątek i szczegóły ogłoszenia). */
export interface JobStepView {
  /** Notka nad przyciskami; `highlight` – wyróżniona (pomocnik zgłosił wykonanie). */
  note: { text: string; icon: string; highlight: boolean } | null;
  /** Przycisk kroku: „Potwierdź wykonanie” jest zielony, akcja awaryjna – w obrysie. */
  action: { label: string; icon: string; color: 'primary' | 'success'; fill: 'solid' | 'outline' } | null;
  /** „Zrezygnuj” / „Przerwij zlecenie”. */
  exit: { label: string; icon: string } | null;
}

export function jobStepView(step: JobStep): JobStepView {
  return {
    note: step.note
      ? { text: step.note, icon: step.highlight ? 'flag-outline' : 'hourglass-outline', highlight: step.highlight }
      : null,
    action: step.action
      ? {
          label: JOB_ACTION_LABELS[step.action],
          icon: JOB_ACTION_ICONS[step.action],
          color: step.action === 'complete' ? 'success' : 'primary',
          fill: step.primary ? 'solid' : 'outline',
        }
      : null,
    exit: step.exit ? { label: JOB_EXIT_LABELS[step.exit], icon: JOB_EXIT_ICONS[step.exit] } : null,
  };
}

/** Rejestruje ikony, których używa `jobStepView` (wołane w konstruktorze ekranu z krokiem zlecenia). */
export function registerJobStepIcons(): void {
  addIcons({ checkmarkDoneOutline, exitOutline, flagOutline, hourglassOutline, playOutline, stopCircleOutline });
}
