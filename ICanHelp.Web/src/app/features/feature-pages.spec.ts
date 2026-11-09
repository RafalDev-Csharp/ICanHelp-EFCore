import { provideHttpClient } from '@angular/common/http';
import type { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ConversationsPage } from './conversations/conversations.page';
import { NotificationsPage } from './notifications/notifications.page';
import { ProfilePage } from './profile/profile.page';

// Strony zakładek z Kroku F1 (na razie puste stany). Kolejne kroki dają stronom własne, dokładniejsze testy
// (Ogłoszenia – F3: `announcements/announcements.page.spec.ts`, Dodaj – F4: `announcements/add-announcement.page.spec.ts`).
const pages: [string, Type<unknown>, string][] = [
  ['ConversationsPage', ConversationsPage, 'Rozmowy'],
  ['NotificationsPage', NotificationsPage, 'Powiadomienia'],
  ['ProfilePage', ProfilePage, 'Profil'],
];

describe.each(pages)('%s', (_name, page, title) => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
  });

  it('renders its title', () => {
    const fixture = TestBed.createComponent(page);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('ion-title')?.textContent?.trim()).toBe(title);
  });
});
