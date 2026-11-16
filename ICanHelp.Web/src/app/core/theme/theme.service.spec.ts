import { TestBed } from '@angular/core/testing';

import { DARK_CLASS, THEME_STORAGE_KEY, ThemeService, parseThemeMode } from './theme.service';

describe('ThemeService', () => {
  let systemDark = false;
  let onSystemChange: ((event: { matches: boolean }) => void) | undefined;
  const originalMatchMedia = window.matchMedia;

  beforeEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.classList.remove(DARK_CLASS);
    systemDark = false;
    onSystemChange = undefined;
    window.matchMedia = ((query: string) => ({
      matches: systemDark,
      media: query,
      addEventListener: (_: string, listener: (event: { matches: boolean }) => void) => (onSystemChange = listener),
      removeEventListener: () => undefined,
    })) as unknown as typeof window.matchMedia;
  });

  afterEach(() => {
    window.matchMedia = originalMatchMedia;
    document.documentElement.classList.remove(DARK_CLASS);
    localStorage.removeItem(THEME_STORAGE_KEY);
  });

  const isDark = (): boolean => document.documentElement.classList.contains(DARK_CLASS);

  it('follows the system by default, also when it changes', () => {
    systemDark = true;
    const theme = TestBed.inject(ThemeService);

    expect(theme.mode()).toBe('system');
    expect(isDark()).toBe(true);

    onSystemChange?.({ matches: false });
    expect(isDark()).toBe(false);
  });

  it('forces the chosen theme and remembers it on the device', () => {
    systemDark = true;
    const theme = TestBed.inject(ThemeService);

    theme.setMode('light');
    expect(isDark()).toBe(false);
    expect(document.documentElement.style.colorScheme).toBe('light');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light');

    onSystemChange?.({ matches: true });
    expect(isDark()).toBe(false);

    theme.setMode('system');
    expect(isDark()).toBe(true);
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
  });

  it('starts with the saved theme', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');

    const theme = TestBed.inject(ThemeService);

    expect(theme.mode()).toBe('dark');
    expect(isDark()).toBe(true);
  });

  it('treats unknown saved values as the system theme', () => {
    expect(parseThemeMode('blue')).toBe('system');
    expect(parseThemeMode(null)).toBe('system');
    expect(parseThemeMode('dark')).toBe('dark');
  });
});
