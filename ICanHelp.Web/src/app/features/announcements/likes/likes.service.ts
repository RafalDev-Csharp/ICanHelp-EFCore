import { Injectable, computed, effect, inject, signal } from '@angular/core';
import type { Observable } from 'rxjs';
import { finalize, map, of, shareReplay, switchMap, tap, throwError } from 'rxjs';

import type { LikedAnnouncementDto } from '../../../core/api/api-client';
import { AnnouncementsClient, UsersClient } from '../../../core/api/api-client';
import { AuthService } from '../../../core/auth/auth.service';

/**
 * Polubione ogłoszenia zalogowanego użytkownika: jedna lista na sesję (`GET api/users/{id}/liked-announcements`),
 * serduszko na kartach i w szczegółach, zakładka „Polubione” w profilu. Zmiany optymistyczne – przy błędzie cofamy.
 */
@Injectable({ providedIn: 'root' })
export class LikesService {
  private readonly users = inject(UsersClient);
  private readonly announcements = inject(AnnouncementsClient);
  private readonly auth = inject(AuthService);

  private readonly state = signal<LikedAnnouncementDto[] | null>(null);
  private request: Observable<LikedAnnouncementDto[]> | null = null;

  /** Od najnowszego polubienia. */
  readonly liked = computed(() => [...(this.state() ?? [])].sort((a, b) => b.likedAt.localeCompare(a.likedAt)));
  private readonly ids = computed(() => new Set((this.state() ?? []).map((like) => like.announcementId)));

  constructor() {
    // Wylogowanie (także w innej karcie) = koniec cudzych polubień w pamięci.
    effect(() => {
      if (!this.auth.isAuthenticated()) {
        this.state.set(null);
      }
    });
  }

  isLiked(announcementId: string): boolean {
    return this.ids().has(announcementId);
  }

  /** Wczytuje polubienia (raz na sesję; `force` – od nowa). Niezalogowany → pusta lista bez żądania. */
  load(force = false): Observable<LikedAnnouncementDto[]> {
    if (!this.auth.isAuthenticated()) {
      return of([]);
    }
    const loaded = this.state();
    if (loaded && !force) {
      return of(loaded);
    }
    this.request ??= this.currentUserId().pipe(
      switchMap((userId) => this.users.getLikedAnnouncements(userId)),
      tap((likes) => this.state.set(likes)),
      finalize(() => (this.request = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.request;
  }

  /** Stan z serwera (np. `isLiked` w szczegółach) – bez żądania. */
  sync(announcementId: string, liked: boolean): void {
    if (this.state() !== null && this.isLiked(announcementId) !== liked) {
      this.apply(announcementId, liked);
    }
  }

  /** Polub / cofnij polubienie (najpierw wczytuje listę, żeby znać stan). Wynik: nowy stan. */
  toggle(announcementId: string): Observable<boolean> {
    return this.load().pipe(
      switchMap(() => {
        const liked = !this.isLiked(announcementId);
        this.apply(announcementId, liked);
        const request = liked ? this.announcements.like(announcementId) : this.announcements.unlike(announcementId);
        return request.pipe(
          map(() => liked),
          // Komunikat pokazał interceptor – tu tylko cofamy serduszko.
          tap({ error: () => this.apply(announcementId, !liked) }),
        );
      }),
    );
  }

  private apply(announcementId: string, liked: boolean): void {
    const current = (this.state() ?? []).filter((like) => like.announcementId !== announcementId);
    const userId = this.auth.currentUser()?.id ?? '';
    this.state.set(liked ? [...current, { userId, announcementId, likedAt: new Date().toISOString() }] : current);
  }

  private currentUserId(): Observable<string> {
    const user = this.auth.currentUser();
    if (user) {
      return of(user.id);
    }
    return this.auth
      .loadCurrentUser()
      .pipe(
        switchMap((loaded) => (loaded ? of(loaded.id) : throwError(() => new Error('Brak zalogowanego użytkownika.')))),
      );
  }
}
