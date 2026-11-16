import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { notificationsStub, provideNotificationsStub } from '../testing/notifications-stub';

import { reloadOnActivity } from './reload-on-activity';

describe('reloadOnActivity', () => {
  const reload = vi.fn();

  @Component({ selector: 'app-reload-test-screen', template: '' })
  class TestScreenComponent {
    constructor() {
      reloadOnActivity((activity) => activity.announcementId === 'a1', reload);
    }
  }

  beforeEach(() => vi.clearAllMocks());

  it('reloads the screen only for notifications that concern it, until the screen is destroyed', () => {
    const notifications = notificationsStub();
    TestBed.configureTestingModule({ providers: [provideNotificationsStub(notifications)] });
    const fixture = TestBed.createComponent(TestScreenComponent);

    notifications.activity$.next({ announcementId: 'other' });
    expect(reload).not.toHaveBeenCalled();

    notifications.activity$.next({ announcementId: 'a1', conversationId: 'c1' });
    expect(reload).toHaveBeenCalledTimes(1);

    fixture.destroy();
    notifications.activity$.next({ announcementId: 'a1' });
    expect(reload).toHaveBeenCalledTimes(1);
  });
});
