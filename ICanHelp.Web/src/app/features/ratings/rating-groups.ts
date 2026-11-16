import type { MyRatingDto } from '../../core/api/api-client';
import { formatAverage, fromRatersSuffix, ratingsCountLabel } from '../../shared/text/rating-texts';

/** F11 H: oceny od jednej osoby – „ocena grupowa” = średnia jej ocen (2 i 4 → 3), jeden głos w średniej. */
export interface RaterGroup {
  raterId: string;
  userName: string;
  /** Najnowsza pierwsza. */
  ratings: MyRatingDto[];
  average: number;
}

function mean(values: readonly number[]): number {
  return values.length === 0 ? 0 : values.reduce((sum, value) => sum + value, 0) / values.length;
}

/** Grupuje oceny po oceniającym; grupy w kolejności najnowszej oceny (lista z API jest od najnowszych). */
export function groupByRater(ratings: readonly MyRatingDto[]): RaterGroup[] {
  const groups = new Map<string, RaterGroup>();
  for (const rating of ratings) {
    const group = groups.get(rating.rater.id);
    if (group) {
      group.ratings.push(rating);
    } else {
      groups.set(rating.rater.id, {
        raterId: rating.rater.id,
        userName: rating.rater.userName,
        ratings: [rating],
        average: 0,
      });
    }
  }
  return [...groups.values()].map((group) => ({ ...group, average: mean(group.ratings.map((r) => r.rate)) }));
}

/** Średnia „jedna osoba = jeden głos”: średnia z ocen grupowych (jak na profilu, bez reguł tempa – te liczy API). */
export function groupedAverage(groups: readonly RaterGroup[]): number {
  return mean(groups.map((group) => group.average));
}

/** „4,0 · 3 oceny od 2 osób” – podsumowanie (jedna osoba = jeden głos; „od N osób”, gdy mniej niż ocen). */
export function ratingsSummary(groups: readonly RaterGroup[]): string {
  const count = groups.reduce((sum, group) => sum + group.ratings.length, 0);
  if (count === 0) {
    return '';
  }
  const average = formatAverage(groupedAverage(groups));
  return `${average} · ${ratingsCountLabel(count)}${fromRatersSuffix(groups.length, count)}`;
}

/** „Ocena grupowa · 2 oceny, średnio 3,0”. */
export function raterGroupLabel(group: RaterGroup): string {
  return `Ocena grupowa · ${ratingsCountLabel(group.ratings.length)}, średnio ${formatAverage(group.average)}`;
}
