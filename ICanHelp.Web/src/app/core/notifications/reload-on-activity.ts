import { inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';

import type { NotificationActivity } from './notification-model';
import { NotificationsService } from './notifications.service';

/**
 * Otwarty ekran przeładowuje dane, gdy przyjdzie powiadomienie, które go dotyczy (`activity$` – z huba albo z pusha
 * przy otwartej aplikacji), np. druga strona zmieniła ofertę albo pomocnik zgłosił wykonanie. Wołać w konstruktorze
 * ekranu (kontekst wstrzykiwania) – nasłuch kończy się razem z ekranem.
 *
 * @param concerns czy powiadomienie dotyczy tego ekranu (jego ogłoszenia / wątku)
 */
export function reloadOnActivity(concerns: (activity: NotificationActivity) => boolean, reload: () => void): void {
  inject(NotificationsService)
    .activity$.pipe(filter(concerns), takeUntilDestroyed())
    .subscribe(() => reload());
}
