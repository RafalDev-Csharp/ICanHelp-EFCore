import { Injectable, inject } from '@angular/core';
import { ToastController } from '@ionic/angular';

/** Ten sam komunikat w tym oknie czasu pokazujemy raz (np. kilka równoległych żądań bez internetu). */
const DUPLICATE_WINDOW_MS = 3_000;

/** Krótkie komunikaty u góry ekranu (Ionic toast). */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly toasts = inject(ToastController);
  private lastMessage = '';
  private lastShownAt = 0;

  async error(message: string): Promise<void> {
    await this.show(message, 'danger');
  }

  async success(message: string): Promise<void> {
    await this.show(message, 'success');
  }

  /** Neutralna informacja (np. „ta funkcja pojawi się wkrótce”). */
  async info(message: string): Promise<void> {
    await this.show(message, 'dark');
  }

  /** Informacja z przyciskiem akcji (np. nowe powiadomienie → „Pokaż”). */
  async action(message: string, actionText: string, onAction: () => void): Promise<void> {
    await this.show(message, 'dark', [
      { text: actionText, handler: onAction },
      { text: 'OK', role: 'cancel' },
    ]);
  }

  private async show(
    message: string,
    color: 'danger' | 'success' | 'dark',
    buttons: { text: string; role?: 'cancel'; handler?: () => void }[] = [{ text: 'OK', role: 'cancel' }],
  ): Promise<void> {
    const now = Date.now();
    if (message === this.lastMessage && now - this.lastShownAt < DUPLICATE_WINDOW_MS) {
      return;
    }
    this.lastMessage = message;
    this.lastShownAt = now;

    const toast = await this.toasts.create({
      message,
      color,
      duration: 4_000,
      position: 'top',
      buttons,
    });
    await toast.present();
  }
}
