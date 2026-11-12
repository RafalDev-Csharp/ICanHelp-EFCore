import { addIcons } from 'ionicons';
import {
  chatbubbleEllipsesOutline,
  colorPaletteOutline,
  flashOutline,
  gridOutline,
  hammerOutline,
  waterOutline,
} from 'ionicons/icons';

/** Ikona kategorii, gdy nie ma jej w mapie (nowa kategoria z seeda). */
export const DEFAULT_CATEGORY_ICON = 'grid-outline';

/**
 * Kafelki kategorii na zakładce „Szukaj” – placeholdery z Ionicons po nazwie kategorii z seeda backendu
 * (`DbInitializer`). Docelowe kategorie i ikony – później (wtedy najlepiej pole `icon` w `CategoryDto`).
 */
const CATEGORY_ICONS: Readonly<Record<string, string>> = {
  plumbing: 'water-outline',
  painting: 'color-palette-outline',
  assembly: 'hammer-outline',
  electrical: 'flash-outline',
  advice: 'chatbubble-ellipses-outline',
};

export function categoryIcon(name: string | null | undefined): string {
  return CATEGORY_ICONS[(name ?? '').trim().toLowerCase()] ?? DEFAULT_CATEGORY_ICON;
}

/** Rejestruje ikony z mapy (wołane w konstruktorze strony z kafelkami). */
export function registerCategoryIcons(): void {
  addIcons({ waterOutline, colorPaletteOutline, hammerOutline, flashOutline, chatbubbleEllipsesOutline, gridOutline });
}
