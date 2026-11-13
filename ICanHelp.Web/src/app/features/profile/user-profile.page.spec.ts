import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { RoleStatsDto, UserProfileDto } from '../../core/api/api-client';
import { UsersClient } from '../../core/api/api-client';

import { UserProfilePage, completedValue, ratingLabel, statRows } from './user-profile.page';

function stats(overrides: Partial<RoleStatsDto> = {}): RoleStatsDto {
  return {
    averageRating: undefined,
    ratingCount: 0,
    raterCount: 0,
    completedCount: 0,
    partnerCount: 0,
    mostJobsWithOnePartner: 0,
    interruptedByUser: 0,
    interruptedByOther: 0,
    abandonedByUser: 0,
    abandonedByOther: 0,
    ...overrides,
  };
}

function profile(overrides: Partial<UserProfileDto> = {}): UserProfileDto {
  return {
    user: {
      id: 'u1',
      userName: 'jan',
      fullName: 'Jan Kowalski',
      description: undefined,
      city: 'Augustów',
      createdAt: '2026-01-10T00:00:00Z',
    },
    asHelper: stats({
      averageRating: 4.65,
      ratingCount: 12,
      raterCount: 9,
      completedCount: 14,
      partnerCount: 9,
      mostJobsWithOnePartner: 3,
      interruptedByUser: 1,
    }),
    asClient: stats({ completedCount: 2 }),
    interruptions: [
      {
        announcementId: 'a1',
        announcementTitle: 'Malowanie płotu',
        role: 'Helper',
        byUser: true,
        exit: {
          number: 1,
          kind: 'Interrupted',
          offerId: 'o1',
          helperId: 'u1',
          actorId: 'u1',
          actorRole: 'Helper',
          reason: 'Disagreement',
          note: 'Inny zakres.',
          occurredAt: '2026-11-13T10:00:00Z',
          response: 'Zakres był w ogłoszeniu.',
          respondedAt: '2026-11-13T12:00:00Z',
        },
      },
    ],
    ...overrides,
  };
}

describe('UserProfilePage', () => {
  const users = { getProfile: vi.fn() };

  function create(): { page: UserProfilePage; element: HTMLElement; render: () => Promise<void> } {
    TestBed.configureTestingModule({
      imports: [UserProfilePage],
      providers: [provideRouter([]), { provide: UsersClient, useValue: users }],
    });
    const fixture = TestBed.createComponent(UserProfilePage);
    fixture.componentRef.setInput('userId', 'u1');
    fixture.componentInstance.ionViewWillEnter();
    fixture.detectChanges();
    return {
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: async () => {
        fixture.detectChanges();
        await fixture.whenStable();
      },
    };
  }

  beforeEach(() => vi.clearAllMocks());

  it('shows the helper statistics and the interruptions with both statements', async () => {
    users.getProfile.mockReturnValue(of(profile()));
    const { element, render } = create();
    await render();

    expect(users.getProfile).toHaveBeenCalledWith('u1');
    expect(element.querySelector('h1')?.textContent).toContain('Jan Kowalski');
    const statsText = element.querySelector('[data-testid="profile-stats"]')?.textContent ?? '';
    expect(statsText).toContain('4,65 / 5 · 12 ocen od 9 osób');
    expect(statsText).toContain('Zakończone zlecenia');
    expect(statsText).toContain('14 · z 9 osobami');
    expect(statsText).toContain('Zlecenia z tą samą osobą');
    const entry = element.querySelector('[data-testid="profile-interruption"]')?.textContent ?? '';
    expect(entry).toContain('Malowanie płotu');
    expect(entry).toContain('Pomagał · przerwał sam');
    expect(entry).toContain('Konflikt / nie dogadaliśmy się – Inny zakres.');
    expect(entry).toContain('Stanowisko zleceniodawcy: Zakres był w ogłoszeniu.');
  });

  it('switches to the client statistics', async () => {
    users.getProfile.mockReturnValue(of(profile({ interruptions: [] })));
    const { page, element, render } = create();
    await render();

    page.selectRole('Client');
    await render();

    expect(element.querySelector('[data-testid="profile-stats"]')?.textContent).toContain('Jeszcze bez ocen');
    expect(element.textContent).toContain('Brak przerwanych zleceń.');
  });

  it('shows not-found for an unknown user', async () => {
    users.getProfile.mockReturnValue(throwError(() => ({ status: 404 })));
    const { element, render } = create();
    await render();

    expect(element.querySelector('app-empty-state')?.textContent).toContain('Nie ma takiego użytkownika');
  });

  it('describes statistics in rows, warning about own interruptions', () => {
    const rows = statRows(stats({ interruptedByUser: 2, abandonedByOther: 1 }), 'Client');
    expect(rows.find((row) => row.label === 'Przerwane przez niego')).toEqual({
      label: 'Przerwane przez niego',
      value: '2',
      warn: true,
    });
    expect(rows.map((row) => row.label)).toContain('Przerwane przez pomocnika');
    expect(rows.map((row) => row.label)).not.toContain('Zlecenia z tą samą osobą');
    expect(rows[0].value).toBe('0');
  });

  it('counts one rating per person in the label (F11 H)', () => {
    expect(ratingLabel(stats())).toBe('Jeszcze bez ocen');
    expect(ratingLabel(stats({ averageRating: 5, ratingCount: 1, raterCount: 1 }))).toBe('5,0 / 5 · 1 ocena');
    expect(ratingLabel(stats({ averageRating: 4.8, ratingCount: 3, raterCount: 1 }))).toBe(
      '4,8 / 5 · 3 oceny od 1 osoby',
    );
    // Oceny są, ale żadna nie weszła do średniej (np. szybkie potwierdzenie) – bez sugerowania oszustwa.
    expect(ratingLabel(stats({ ratingCount: 2 }))).toBe('Średnia jeszcze niepoliczona · 2 oceny');
  });

  it('shows the completed jobs with the number of different partners', () => {
    expect(completedValue(stats({ completedCount: 1, partnerCount: 1 }))).toBe('1 · z 1 osobą');
    expect(completedValue(stats({ completedCount: 5, partnerCount: 2 }))).toBe('5 · z 2 osobami');
    const rows = statRows(stats({ completedCount: 3, partnerCount: 1, mostJobsWithOnePartner: 3 }), 'Helper');
    expect(rows[1]).toEqual({ label: 'Zlecenia z tą samą osobą', value: '3', warn: false });
  });
});
