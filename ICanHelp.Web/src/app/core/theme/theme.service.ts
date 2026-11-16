import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';

/** Motyw wybrany w „Konto → Aplikacja”: jak w systemie telefonu, zawsze jasny albo zawsze ciemny. */
export type ThemeMode = 'system' | 'light' | 'dark';

/** Klucz w `localStorage` – ten sam czyta skrypt w `index.html` (motyw przed startem Angulara, bez mignięcia). */
export const THEME_STORAGE_KEY = 'icanhelp.theme';

/** Klasa palety ciemnej Ionic (`palettes/dark.class.css` w `global.scss`). */
export const DARK_CLASS = 'ion-palette-dark';

const DARK_QUERY = '(prefers-color-scheme: dark)';

export function parseThemeMode(value: string | null | undefined): ThemeMode {
  return value === 'light' || value === 'dark' ? value : 'system';
}

/**
 * Jasny / ciemny motyw aplikacji. Wybór pamiętamy na urządzeniu (`localStorage`), nie na koncie – na telefonie
 * i komputerze można mieć inny. „Systemowy” śledzi ustawienie systemu także w trakcie działania aplikacji.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly media = this.document.defaultView?.matchMedia?.(DARK_QUERY) ?? null;
  private readonly systemDark = signal(this.media?.matches ?? false);

  readonly mode = signal<ThemeMode>(parseThemeMode(this.read()));
  readonly dark = computed(() => (this.mode() === 'system' ? this.systemDark() : this.mode() === 'dark'));

  constructor() {
    this.media?.addEventListener?.('change', (event) => {
      this.systemDark.set(event.matches);
      this.apply();
    });
    this.apply();
  }

  setMode(mode: ThemeMode): void {
    this.mode.set(mode);
    try {
      if (mode === 'system') {
        localStorage.removeItem(THEME_STORAGE_KEY);
      } else {
        localStorage.setItem(THEME_STORAGE_KEY, mode);
      }
    } catch {
      // tryb prywatny / pełny magazyn – motyw działa do końca tej sesji
    }
    this.apply();
  }

  private apply(): void {
    const root = this.document.documentElement;
    const dark = this.dark();
    root.classList.toggle(DARK_CLASS, dark);
    // Paski przewijania i natywne pola przeglądarki w tym samym motywie.
    root.style.colorScheme = dark ? 'dark' : 'light';
  }

  private read(): string | null {
    try {
      return localStorage.getItem(THEME_STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
