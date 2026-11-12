import type { Provider, WritableSignal } from '@angular/core';
import { signal } from '@angular/core';
import { Subject } from 'rxjs';

import type { NotificationDto } from '../api/api-client';
import { NotificationsService } from '../notifications/notifications.service';

export interface NotificationsStub {
  unreadCount: WritableSignal<number>;
  received$: Subject<NotificationDto>;
}

/**
 * Zaślepka `NotificationsService` dla stron z dzwonkiem w nagłówku (`app-notifications-bell`) albo odświeżaniem po
 * powiadomieniu (`received$`) – prawdziwy serwis łączyłby się z hubem SignalR.
 */
export function provideNotificationsStub(stub: NotificationsStub = notificationsStub()): Provider {
  return { provide: NotificationsService, useValue: stub };
}

export function notificationsStub(): NotificationsStub {
  return { unreadCount: signal(0), received$: new Subject<NotificationDto>() };
}
