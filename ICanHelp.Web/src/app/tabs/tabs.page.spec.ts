import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { NavController } from '@ionic/angular';

import { NotificationsService } from '../core/notifications/notifications.service';
import { PushService } from '../core/pwa/push.service';

import { TabsPage } from './tabs.page';

describe('TabsPage', () => {
  const notifications = { unreadCount: signal(0), start: vi.fn() };
  const push = { start: vi.fn() };

  beforeEach(async () => {
    notifications.unreadCount.set(0);
    notifications.start.mockClear();
    push.start.mockClear();
    await TestBed.configureTestingModule({
      imports: [TabsPage],
      providers: [
        provideRouter([]),
        { provide: NotificationsService, useValue: notifications },
        { provide: PushService, useValue: push },
      ],
    }).compileComponents();
  });

  it('starts live notifications and shows the unread badge', () => {
    const fixture = TestBed.createComponent(TabsPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(notifications.start).toHaveBeenCalledTimes(1);
    expect(push.start).toHaveBeenCalledTimes(1);
    expect(element.querySelector('ion-badge')).toBeNull();

    notifications.unreadCount.set(3);
    fixture.detectChanges();
    expect(element.querySelector('ion-tab-button[tab="notifications"] ion-badge')?.getAttribute('aria-label')).toBe(
      '3 nieprzeczytanych',
    );

    notifications.unreadCount.set(150);
    expect(fixture.componentInstance.unreadBadge()).toBe('99+');
  });

  it('opens the root of the tapped tab instead of its last screen', () => {
    const fixture = TestBed.createComponent(TabsPage);
    fixture.detectChanges();
    const navCtrl = TestBed.inject(NavController);
    const navigateRoot = vi.spyOn(navCtrl, 'navigateRoot').mockResolvedValue(true);
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'url', 'get').mockReturnValue('/announcements/a1');

    const event = new CustomEvent('ionTabButtonClick', { detail: { tab: 'announcements' }, cancelable: true });
    const stop = vi.spyOn(event, 'stopPropagation');
    fixture.componentInstance.openTabRoot(event);

    expect(stop).toHaveBeenCalled();
    expect(navigateRoot).toHaveBeenCalledWith('/announcements', { animated: true, animationDirection: 'back' });

    // Już na korzeniu – nic nie robimy.
    navigateRoot.mockClear();
    vi.spyOn(router, 'url', 'get').mockReturnValue('/profile?x=1');
    fixture.componentInstance.openTabRoot(new CustomEvent('ionTabButtonClick', { detail: { tab: 'profile' } }));
    expect(navigateRoot).not.toHaveBeenCalled();
  });

  // Teksty etykiet sprawdza e2e (e2e/app.spec.ts) – w jsdom komponenty Ionic czyszczą treść `ion-label`.
  it('links the five tabs to their routes', () => {
    const fixture = TestBed.createComponent(TabsPage);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const tabs = Array.from(element.querySelectorAll('ion-tab-button')).map((button) => [
      button.getAttribute('tab'),
      button.getAttribute('href'),
    ]);
    expect(tabs).toEqual([
      ['announcements', '/announcements'],
      ['add', '/add'],
      ['conversations', '/conversations'],
      ['notifications', '/notifications'],
      ['profile', '/profile'],
    ]);
  });
});
