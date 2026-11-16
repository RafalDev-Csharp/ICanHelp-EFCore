import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { NotificationsService } from '../../../core/notifications/notifications.service';

import { NotificationsBellComponent, unreadBadgeText } from './notifications-bell.component';

describe('NotificationsBellComponent', () => {
  const notifications = { unreadCount: signal(0) };
  const auth = { isAuthenticated: signal(true) };

  beforeEach(async () => {
    notifications.unreadCount.set(0);
    auth.isAuthenticated.set(true);
    await TestBed.configureTestingModule({
      imports: [NotificationsBellComponent],
      providers: [
        provideRouter([]),
        { provide: NotificationsService, useValue: notifications },
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();
  });

  it('formats the badge', () => {
    expect(unreadBadgeText(0)).toBe('');
    expect(unreadBadgeText(-1)).toBe('');
    expect(unreadBadgeText(7)).toBe('7');
    expect(unreadBadgeText(150)).toBe('99+');
  });

  it('links to notifications and shows the unread badge', () => {
    const fixture = TestBed.createComponent(NotificationsBellComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const button = element.querySelector('ion-button');
    expect(button?.getAttribute('aria-label')).toBe('Powiadomienia');
    expect(element.querySelector('ion-badge')).toBeNull();

    notifications.unreadCount.set(3);
    fixture.detectChanges();
    expect(element.querySelector('ion-badge')?.textContent?.trim()).toBe('3');
    expect(element.querySelector('ion-button')?.getAttribute('aria-label')).toBe('Powiadomienia, 3 nieprzeczytanych');
  });

  it('is hidden for anonymous users', () => {
    auth.isAuthenticated.set(false);
    const fixture = TestBed.createComponent(NotificationsBellComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('ion-button')).toBeNull();
  });
});
