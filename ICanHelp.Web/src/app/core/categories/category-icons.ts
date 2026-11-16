import { addIcons } from 'ionicons';
import {
  bedOutline,
  carOutline,
  chatbubbleEllipsesOutline,
  colorPaletteOutline,
  cubeOutline,
  desktopOutline,
  flashOutline,
  gridOutline,
  hammerOutline,
  leafOutline,
  schoolOutline,
  sparklesOutline,
  waterOutline,
} from 'ionicons/icons';

/** Ikona kategorii, gdy nie ma jej w mapie (nowa kategoria z seeda). */
export const DEFAULT_CATEGORY_ICON = 'grid-outline';

/**
 * Kafelki kategorii na zakładce „Szukaj” – ikony z Ionicons po nazwie kategorii z seeda backendu
 * (`DbInitializer`, klucze małymi literami). Docelowo najlepiej pole `icon` w `CategoryDto`.
 */
const CATEGORY_ICONS: Readonly<Record<string, string>> = {
  hydraulika: 'water-outline',
  elektryka: 'flash-outline',
  'drobne naprawy domowe': 'hammer-outline',
  'malowanie i wykończenia': 'color-palette-outline',
  'meble i montaż': 'bed-outline',
  'ogród i podwórko': 'leaf-outline',
  sprzątanie: 'sparkles-outline',
  'przeprowadzki i transport': 'cube-outline',
  'auto i rower': 'car-outline',
  'komputery i elektronika': 'desktop-outline',
  'korepetycje i nauka': 'school-outline',
  'porady i pomoc codzienna': 'chatbubble-ellipses-outline',
};

export function categoryIcon(name: string | null | undefined): string {
  return CATEGORY_ICONS[(name ?? '').trim().toLowerCase()] ?? DEFAULT_CATEGORY_ICON;
}

/** Rejestruje ikony z mapy (wołane w konstruktorze strony z kafelkami). */
export function registerCategoryIcons(): void {
  addIcons({
    bedOutline,
    carOutline,
    chatbubbleEllipsesOutline,
    colorPaletteOutline,
    cubeOutline,
    desktopOutline,
    flashOutline,
    gridOutline,
    hammerOutline,
    leafOutline,
    schoolOutline,
    sparklesOutline,
    waterOutline,
  });
}
