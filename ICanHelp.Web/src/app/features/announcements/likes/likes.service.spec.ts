import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError } from 'rxjs';

import type { UserDto } from '../../../core/api/api-client';
import { AnnouncementsClient, UsersClient } from '../../../core/api/api-client';
import { AuthService } from '../../../core/auth/auth.service';

import { LikesService } from './likes.service';

describe('LikesService', () => {
  const users = { getLikedAnnouncements: vi.fn() };
  const announcements = { like: vi.fn(), unlike: vi.fn() };
  const auth = {
    isAuthenticated: signal(true),
    currentUser: signal<UserDto | null>({ id: 'u1' } as UserDto),
    loadCurrentUser: vi.fn(),
  };

  function create(): LikesService {
    TestBed.configureTestingModule({
      providers: [
        { provide: UsersClient, useValue: users },
        { provide: AnnouncementsClient, useValue: announcements },
        { provide: AuthService, useValue: auth },
      ],
    });
    return TestBed.inject(LikesService);
  }

  beforeEach(() => {
    vi.resetAllMocks();
    auth.isAuthenticated.set(true);
    auth.currentUser.set({ id: 'u1' } as UserDto);
    users.getLikedAnnouncements.mockReturnValue(
      of([{ userId: 'u1', announcementId: 'a1', likedAt: '2026-11-01T10:00:00Z' }]),
    );
  });

  it('loads the likes of the signed-in user once', async () => {
    const service = create();

    await firstValueFrom(service.load());
    await firstValueFrom(service.load());

    expect(users.getLikedAnnouncements).toHaveBeenCalledTimes(1);
    expect(users.getLikedAnnouncements).toHaveBeenCalledWith('u1');
    expect(service.isLiked('a1')).toBe(true);
    expect(service.isLiked('a2')).toBe(false);
  });

  it('does not ask the API for a guest', async () => {
    auth.isAuthenticated.set(false);
    const service = create();

    expect(await firstValueFrom(service.load())).toEqual([]);
    expect(users.getLikedAnnouncements).not.toHaveBeenCalled();
  });

  it('likes and unlikes', async () => {
    announcements.like.mockReturnValue(of(undefined));
    announcements.unlike.mockReturnValue(of(undefined));
    const service = create();

    expect(await firstValueFrom(service.toggle('a2'))).toBe(true);
    expect(announcements.like).toHaveBeenCalledWith('a2');
    expect(service.isLiked('a2')).toBe(true);
    expect(service.liked()[0].announcementId).toBe('a2');

    expect(await firstValueFrom(service.toggle('a1'))).toBe(false);
    expect(announcements.unlike).toHaveBeenCalledWith('a1');
    expect(service.isLiked('a1')).toBe(false);
  });

  it('reverts the heart when the API fails', async () => {
    announcements.like.mockReturnValue(throwError(() => ({ status: 404 })));
    const service = create();

    await expect(firstValueFrom(service.toggle('a2'))).rejects.toEqual({ status: 404 });
    expect(service.isLiked('a2')).toBe(false);
  });
});
