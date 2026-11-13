import type { Provider, WritableSignal } from '@angular/core';
import { signal } from '@angular/core';
import { Subject } from 'rxjs';

import type { NotificationDto } from '../api/api-client';
import type { NotificationActivity } from '../notifications/notifications.service';
import { NotificationsService } from '../notifications/notifications.service';

export interface NotificationsStub {
  unreadCount: WritableSignal<number>;
  received$: Subject<NotificationDto>;
  activity$: Subject<NotificationActivity>;
}

/**
 * Zaślepka `NotificationsService` dla stron z dzwonkiem w nagłówku (`app-notifications-bell`) albo odświeżaniem po
 * powiadomieniu (`received$`, `activity$`) – prawdziwy serwis łączyłby się z hubem SignalR.
 */
export function provideNotificationsStub(stub: NotificationsStub = notificationsStub()): Provider {
  return { provide: NotificationsService, useValue: stub };
}

export function notificationsStub(): NotificationsStub {
  return {
    unreadCount: signal(0),
    received$: new Subject<NotificationDto>(),
    activity$: new Subject<NotificationActivity>(),
  };
}
