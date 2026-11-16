import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { loginUrlTree } from '../../../core/auth/auth.guard';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/ui/toast.service';

import { LikesService } from './likes.service';

/**
 * Serduszko na karcie ogłoszenia (lista „Szukaj”, podgląd na mapie, „Ulubione”): niezalogowany → logowanie i powrót,
 * zalogowany → polub / cofnij z komunikatem. Jedno przełączenie naraz dla danego ogłoszenia.
 */
@Injectable({ providedIn: 'root' })
export class LikeToggleService {
  private readonly likes = inject(LikesService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  private readonly pending = signal<ReadonlySet<string>>(new Set());

  isPending(announcementId: string): boolean {
    return this.pending().has(announcementId);
  }

  /** Właściciel nie polubi własnego ogłoszenia (API i tak odmówi) – nie pokazujemy mu serduszka. */
  canLike(ownerId: string): boolean {
    return this.auth.currentUser()?.id !== ownerId;
  }

  toggle(announcementId: string): void {
    if (this.isPending(announcementId)) {
      return;
    }
    if (!this.auth.isAuthenticated()) {
      void this.router.navigateByUrl(loginUrlTree(this.router, this.router.url));
      return;
    }
    this.setPending(announcementId, true);
    this.likes.toggle(announcementId).subscribe({
      next: (liked) => {
        this.setPending(announcementId, false);
        void this.toast.info(liked ? 'Dodano do ulubionych.' : 'Usunięto z ulubionych.');
      },
      error: () => this.setPending(announcementId, false),
    });
  }

  private setPending(announcementId: string, pending: boolean): void {
    const next = new Set(this.pending());
    if (pending) {
      next.add(announcementId);
    } else {
      next.delete(announcementId);
    }
    this.pending.set(next);
  }
}
