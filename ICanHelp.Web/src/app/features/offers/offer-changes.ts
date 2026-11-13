import type { AnnouncementFieldChangeDto } from '../../core/api/api-client';
import { formatPrice } from '../../shared/pipes/price.pipe';
import { pluralPl } from '../../shared/text/plural';
import { difficultyLabel } from '../announcements/announcement-labels';

/** Jeden wiersz podglądu zmian ogłoszenia: „Budżet: 150 zł → 200 zł”. */
export interface FieldChangeRow {
  label: string;
  before: string;
  after: string;
}

const FIELD_TITLES: Readonly<Record<string, string>> = {
  Category: 'Kategoria',
  Difficulty: 'Trudność',
  Title: 'Tytuł',
  Description: 'Opis',
  Budget: 'Budżet',
  Location: 'Miejsce',
  Images: 'Zdjęcia',
};

/**
 * Wiersze „było → jest” z `AnnouncementFieldChangeDto` (tekst zapisany przez domenę – `Announcement.Update`):
 * kategoria `kategoria/podkategoria` (id → nazwa z `categoryName`), budżet `150.00 PLN`, miejsce jako współrzędne
 * (pokazujemy tylko, że się zmieniło – dokładnego miejsca nie zdradzamy), zdjęcia – lista id (liczba, dodane / usunięte).
 */
export function fieldChangeRows(
  changes: readonly AnnouncementFieldChangeDto[],
  categoryName: (id: string) => string = (id) => id,
): FieldChangeRow[] {
  return changes.map((change) => {
    const label = FIELD_TITLES[change.field] ?? change.field;
    const before = change.oldValue ?? '';
    const after = change.newValue ?? '';
    switch (change.field) {
      case 'Category':
        return { label, before: categoryText(before, categoryName), after: categoryText(after, categoryName) };
      case 'Difficulty':
        return { label, before: difficultyLabel(before), after: difficultyLabel(after) };
      case 'Budget':
        return { label, before: budgetText(before), after: budgetText(after) };
      case 'Location':
        return { label, before: 'poprzednie miejsce', after: 'nowe miejsce na mapie' };
      case 'Images':
        return { label, before: imagesText(ids(before)), after: imagesAfterText(ids(before), ids(after)) };
      default:
        return { label, before: before || '—', after: after || '—' };
    }
  });
}

function categoryText(value: string, categoryName: (id: string) => string): string {
  const names = value
    .split('/')
    .filter(Boolean)
    .map((id) => categoryName(id));
  return names.join(' › ') || '—';
}

function budgetText(value: string): string {
  const amount = Number.parseFloat(value);
  return Number.isFinite(amount) ? formatPrice(amount) : value || '—';
}

function ids(value: string): string[] {
  return value
    .split(',')
    .map((id) => id.trim())
    .filter(Boolean);
}

function imagesText(list: readonly string[]): string {
  return list.length === 0 ? 'bez zdjęć' : `${list.length} ${pluralPl(list.length, 'zdjęcie', 'zdjęcia', 'zdjęć')}`;
}

function imagesAfterText(before: readonly string[], after: readonly string[]): string {
  const added = after.filter((id) => !before.includes(id)).length;
  const removed = before.filter((id) => !after.includes(id)).length;
  const details = [added ? `dodano ${added}` : '', removed ? `usunięto ${removed}` : ''].filter(Boolean).join(', ');
  return details ? `${imagesText(after)} (${details})` : imagesText(after);
}
