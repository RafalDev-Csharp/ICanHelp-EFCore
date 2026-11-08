import type { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AddAnnouncementPage } from './announcements/add-announcement.page';
import { AnnouncementsPage } from './announcements/announcements.page';
import { ConversationsPage } from './conversations/conversations.page';
import { NotificationsPage } from './notifications/notifications.page';
import { ProfilePage } from './profile/profile.page';

// Strony zakładek z Kroku F1 (na razie puste stany). Kolejne kroki dają stronom własne, dokładniejsze testy.
const pages: [string, Type<unknown>, string][] = [
  ['AnnouncementsPage', AnnouncementsPage, 'Ogłoszenia'],
  ['AddAnnouncementPage', AddAnnouncementPage, 'Dodaj ogłoszenie'],
  ['ConversationsPage', ConversationsPage, 'Rozmowy'],
  ['NotificationsPage', NotificationsPage, 'Powiadomienia'],
  ['ProfilePage', ProfilePage, 'Profil'],
];

describe.each(pages)('%s', (_name, page, title) => {
  it('renders its title', () => {
    const fixture = TestBed.createComponent(page);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('ion-title')?.textContent?.trim()).toBe(title);
  });
});
