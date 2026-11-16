import { Injectable, inject } from '@angular/core';

import type { AlertDto } from '../../core/api/api-client';
import { AlertsClient } from '../../core/api/api-client';
import { ApiCommands } from '../../core/api/api-commands';
import { DialogService } from '../../core/ui/dialog.service';

/**
 * Usunięcie alertu – to samo na liście („Moje alerty”) i w edycji: pytanie → `DELETE api/me/alerts/{id}` →
 * komunikat. Ekran sam decyduje, co potem (zdjęcie wiersza z listy / powrót do listy).
 */
@Injectable({ providedIn: 'root' })
export class AlertRemovalService {
  private readonly client = inject(AlertsClient);
  private readonly dialogs = inject(DialogService);
  private readonly commands = inject(ApiCommands);

  /** `true`, gdy użytkownik potwierdził i alert usunięto. */
  async remove(alert: Pick<AlertDto, 'id' | 'name'>): Promise<boolean> {
    const confirmed = await this.dialogs.confirm({
      header: 'Usunąć alert?',
      message: `„${alert.name}” – nie dostaniesz już powiadomień z tego alertu.`,
      confirmText: 'Usuń',
      cancelText: 'Anuluj',
      destructive: true,
    });
    return confirmed && (await this.commands.run(this.client.delete(alert.id), 'Alert usunięty.'));
  }
}
