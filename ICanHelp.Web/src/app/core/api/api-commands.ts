import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { firstValueFrom } from 'rxjs';

import { ToastService } from '../ui/toast.service';

/**
 * Komendy API wołane po akcji użytkownika (przyjęcie oferty, start zlecenia…): żądanie jako obietnica, po sukcesie
 * krótki komunikat. Błędu nie trzeba obsługiwać – komunikat pokazał już interceptor, tu kończy się `false` / `null`.
 */
@Injectable({ providedIn: 'root' })
export class ApiCommands {
  private readonly toast = inject(ToastService);

  /** Komenda bez odpowiedzi: `true`, gdy się udała. */
  async run(request: Observable<unknown>, success: string): Promise<boolean> {
    return (await this.send(request, success)) !== null;
  }

  /** Komenda z odpowiedzią: `{ result }` po sukcesie (także dla `void` z API), `null` przy błędzie. */
  async send<T>(request: Observable<T>, success: string): Promise<{ result: T | undefined } | null> {
    try {
      const result = await firstValueFrom(request, { defaultValue: undefined });
      void this.toast.success(success);
      return { result };
    } catch {
      return null;
    }
  }
}
