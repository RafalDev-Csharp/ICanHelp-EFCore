/** Teksty enumów z API (`DifficultyLevel`, `AnnouncementStatus`) – nieznana wartość zostaje bez tłumaczenia. */
const DIFFICULTY_LABELS: Readonly<Record<string, string>> = {
  Easy: 'Łatwe',
  Medium: 'Średnie',
  Hard: 'Trudne',
};

const STATUS_LABELS: Readonly<Record<string, string>> = {
  Open: 'Czeka na oferty',
  Accepted: 'Oferta przyjęta',
  InProgress: 'W realizacji',
  Completed: 'Zakończone',
  Cancelled: 'Anulowane',
};

const STATUS_COLORS: Readonly<Record<string, string>> = {
  Open: 'success',
  Accepted: 'primary',
  InProgress: 'warning',
  Completed: 'medium',
  Cancelled: 'danger',
};

const DIFFICULTY_COLORS: Readonly<Record<string, string>> = {
  Easy: 'success',
  Medium: 'warning',
  Hard: 'danger',
};

export function difficultyLabel(difficulty: string): string {
  return DIFFICULTY_LABELS[difficulty] ?? difficulty;
}

/** Kolor Ionic etykiety trudności (`ion-chip [color]`); nieznana wartość – szary. */
export function difficultyColor(difficulty: string): string {
  return DIFFICULTY_COLORS[difficulty] ?? 'medium';
}

export function statusLabel(status: string): string {
  return STATUS_LABELS[status] ?? status;
}

/** Kolor Ionic (`ion-badge [color]`) dla statusu ogłoszenia. */
export function statusColor(status: string): string {
  return STATUS_COLORS[status] ?? 'medium';
}
