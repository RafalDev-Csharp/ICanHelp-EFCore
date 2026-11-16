import type { ProfileInterruptionDto, RoleStatsDto, UserProfileDto } from '../../core/api/api-client';
import { APP_LOCALE } from '../../core/config/locale';
import { pluralPl } from '../../shared/text/plural';
import { fromRatersSuffix, ratingsCountLabel } from '../../shared/text/rating-texts';

/** Segment statystyk: rola w zleceniach. */
export type ProfileRole = 'Helper' | 'Client';

/** „4,65” – średnia z dwoma miejscami po przecinku. */
const averageFormat = new Intl.NumberFormat(APP_LOCALE, { minimumFractionDigits: 1, maximumFractionDigits: 2 });

/** Wiersz statystyk do szablonu. */
export interface StatRow {
  label: string;
  value: string;
  /** Wyróżnienie (ostrzeżenie) – sam przerywał / rezygnował. */
  warn: boolean;
}

/** Rola pokazywana na start: ta, w której użytkownik ma więcej zleceń (remis – „Pomagam”). */
export function defaultProfileRole(profile: Pick<UserProfileDto, 'asHelper' | 'asClient'>): ProfileRole {
  const helperJobs = profile.asHelper.completedCount + profile.asHelper.interruptedByUser;
  const clientJobs = profile.asClient.completedCount + profile.asClient.interruptedByUser;
  return clientJobs > helperJobs ? 'Client' : 'Helper';
}

/** „14 · z 9 osobami” – zakończone zlecenia i liczba różnych partnerów (F11 H). */
export function completedValue(stats: RoleStatsDto): string {
  if (stats.completedCount === 0) {
    return '0';
  }
  const partners = stats.partnerCount;
  return `${stats.completedCount} · z ${partners} ${pluralPl(partners, 'osobą', 'osobami', 'osobami')}`;
}

/**
 * Statystyki jednej roli w wierszach („Zakończone zlecenia: 14 · z 9 osobami”, „Przerwane przez niego: 1”…).
 * F11 H: „Zlecenia z tą samą osobą” – tylko gdy z kimś było więcej niż jedno (informacja, bez ostrzeżenia).
 */
export function statRows(stats: RoleStatsDto, role: ProfileRole): StatRow[] {
  const other = role === 'Helper' ? 'zleceniodawcę' : 'pomocnika';
  const rows: StatRow[] = [{ label: 'Zakończone zlecenia', value: completedValue(stats), warn: false }];
  if (stats.mostJobsWithOnePartner > 1) {
    rows.push({ label: 'Zlecenia z tą samą osobą', value: String(stats.mostJobsWithOnePartner), warn: false });
  }
  rows.push(
    { label: 'Przerwane przez niego', value: String(stats.interruptedByUser), warn: stats.interruptedByUser > 0 },
    { label: `Przerwane przez ${other}`, value: String(stats.interruptedByOther), warn: false },
    { label: 'Rezygnacje przed startem', value: String(stats.abandonedByUser), warn: stats.abandonedByUser > 0 },
    { label: `Rezygnacje – ${other}`, value: String(stats.abandonedByOther), warn: false },
  );
  return rows;
}

/**
 * „4,65 / 5 · 12 ocen od 9 osób” albo „Jeszcze bez ocen”. Średnia tylko z zakończonych zleceń, F11 H: jeden głos na
 * osobę (średnia jej ocen), bez podejrzanie szybkich i powtórek – „od N osób” tylko, gdy to mniej niż wszystkich ocen.
 * Oceny są, ale żadna się nie liczy → „Średnia jeszcze niepoliczona” (bez sugerowania oszustwa).
 */
export function ratingLabel(stats: RoleStatsDto): string {
  if (stats.ratingCount === 0) {
    return 'Jeszcze bez ocen';
  }
  const count = ratingsCountLabel(stats.ratingCount);
  if (stats.averageRating === undefined || stats.averageRating === null || stats.raterCount === 0) {
    return `Średnia jeszcze niepoliczona · ${count}`;
  }
  const raters = fromRatersSuffix(stats.raterCount, stats.ratingCount);
  return `${averageFormat.format(stats.averageRating)} / 5 · ${count}${raters}`;
}

/** „Pomagał · przerwał sam” / „Zlecał · przerwał pomocnik” – wiersz listy „Zlecenia przerwane”. */
export function interruptionMeta(item: Pick<ProfileInterruptionDto, 'role' | 'byUser'>): string {
  const role = item.role === 'Helper' ? 'Pomagał' : 'Zlecał';
  const who = item.byUser ? 'przerwał sam' : item.role === 'Helper' ? 'przerwał zleceniodawca' : 'przerwał pomocnik';
  return `${role} · ${who}`;
}
