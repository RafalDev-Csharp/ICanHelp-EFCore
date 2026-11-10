import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

/** Zdarzenie Chrome / Edge na Androidzie (i komputerze) – nie ma go w typach DOM. */
export interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

/**
 * Zachęta do instalacji PWA. Android (Chrome): przechwytujemy `beforeinstallprompt` i pokazujemy własny przycisk
 * „Zainstaluj aplikację”. iPhone (Safari) nie ma takiego zdarzenia – pokazujemy instrukcję („Udostępnij → Do ekranu
 * początkowego”); bez instalacji iPhone nie dostanie pusha. Serwis tworzymy przy starcie (`provideAppInitializer`),
 * bo zdarzenie przychodzi zaraz po wczytaniu strony.
 */
@Injectable({ providedIn: 'root' })
export class InstallPromptService {
  private readonly deferred = signal<BeforeInstallPromptEvent | null>(null);
  private readonly installed = signal(isStandalone());

  /** Działa jako zainstalowana aplikacja (ekran początkowy / okno aplikacji). */
  readonly isStandalone = this.installed.asReadonly();
  readonly isIos = isIos();
  /** Android / komputer: można pokazać systemowe okno instalacji. */
  readonly canPrompt = computed(() => this.deferred() !== null && !this.installed());
  /** iPhone / iPad w Safari, jeszcze nie na ekranie początkowym – pokazujemy instrukcję. */
  readonly showIosHint = computed(() => this.isIos && !this.installed());

  constructor() {
    const onPrompt = (event: Event): void => {
      event.preventDefault(); // bez domyślnego paska – instalacja z naszego przycisku
      this.deferred.set(event as BeforeInstallPromptEvent);
    };
    const onInstalled = (): void => {
      this.deferred.set(null);
      this.installed.set(true);
    };
    window.addEventListener('beforeinstallprompt', onPrompt);
    window.addEventListener('appinstalled', onInstalled);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('beforeinstallprompt', onPrompt);
      window.removeEventListener('appinstalled', onInstalled);
    });
  }

  /** Systemowe okno instalacji (tylko po kliknięciu). Zwraca `true`, gdy użytkownik zainstalował. */
  async install(): Promise<boolean> {
    const event = this.deferred();
    if (!event) {
      return false;
    }
    this.deferred.set(null); // zdarzenia można użyć tylko raz
    await event.prompt();
    const choice = await event.userChoice;
    if (choice.outcome === 'accepted') {
      this.installed.set(true);
      return true;
    }
    return false;
  }
}

export function isStandalone(): boolean {
  const iosStandalone = (navigator as Navigator & { standalone?: boolean }).standalone === true;
  return iosStandalone || window.matchMedia('(display-mode: standalone)').matches;
}

/** iPhone / iPod / iPad (iPadOS udaje komputer Mac, ale ma ekran dotykowy). */
export function isIos(): boolean {
  const userAgent = navigator.userAgent;
  return /iPad|iPhone|iPod/.test(userAgent) || (/Macintosh/.test(userAgent) && navigator.maxTouchPoints > 1);
}
