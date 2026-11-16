import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';

import type { UserDto } from '../../../core/api/api-client';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/ui/toast.service';

import { LikeToggleService } from './like-toggle.service';
import { LikesService } from './likes.service';

describe('LikeToggleService', () => {
  const auth = { isAuthenticated: signal(false), currentUser: signal<UserDto | null>(null) };
  const likes = { toggle: vi.fn() };
  const toast = { info: vi.fn().mockResolvedValue(undefined) };

  function create(): LikeToggleService {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: LikesService, useValue: likes },
        { provide: ToastService, useValue: toast },
      ],
    });
    return TestBed.inject(LikeToggleService);
  }

  beforeEach(() => {
    vi.resetAllMocks();
    toast.info.mockResolvedValue(undefined);
    auth.isAuthenticated.set(false);
    auth.currentUser.set(null);
  });

  it('sends a guest to sign in instead of liking', () => {
    const service = create();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    service.toggle('a1');

    expect(navigate).toHaveBeenCalled();
    expect(likes.toggle).not.toHaveBeenCalled();
  });

  it('toggles once at a time and confirms the new state', () => {
    auth.isAuthenticated.set(true);
    const result = new Subject<boolean>();
    likes.toggle.mockReturnValue(result);
    const service = create();

    service.toggle('a1');
    service.toggle('a1');
    expect(service.isPending('a1')).toBe(true);
    result.next(true);

    expect(likes.toggle).toHaveBeenCalledTimes(1);
    expect(service.isPending('a1')).toBe(false);
    expect(toast.info).toHaveBeenCalledWith('Dodano do ulubionych.');
  });

  it('hides the heart on the own announcement', () => {
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
    likes.toggle.mockReturnValue(of(false));
    const service = create();

    expect(service.canLike('owner-1')).toBe(false);
    expect(service.canLike('someone-else')).toBe(true);
  });
});
