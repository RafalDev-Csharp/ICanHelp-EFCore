import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonItem,
  IonItemOption,
  IonItemOptions,
  IonItemSliding,
  IonLabel,
  IonList,
  IonNote,
  IonRefresher,
  IonRefresherContent,
  IonTitle,
  IonToggle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { locationOutline, navigateOutline, notificationsOutline } from 'ionicons/icons';

import type { AlertDto } from '../../core/api/api-client';
import { AlertsClient } from '../../core/api/api-client';
import { CategoriesService } from '../../core/categories/categories.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { RemoteList } from '../../shared/data/remote-list';

import { ALERT_RULES, describeSchedule, formatRadius } from './alert-form';
import { AlertRemovalService } from './alert-removal.service';

/**
 * „Moje alerty” (profil): powiadomienia o nowych ogłoszeniach w okolicy (obszar) albo po drodze (trasa).
 * Przełącznik = `enable` / `disable` od razu (bez zapisu formularza), przesunięcie w lewo = usuń. Najwyżej 10 alertów.
 */
@Component({
  selector: 'app-alerts',
  templateUrl: './alerts.page.html',
  styleUrls: ['./alerts.page.scss'],
  imports: [
    BrandComponent,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonRefresher,
    IonRefresherContent,
    IonList,
    IonItemSliding,
    IonItem,
    IonItemOptions,
    IonItemOption,
    IonIcon,
    IonLabel,
    IonNote,
    IonToggle,
    IonButton,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
  ],
})
export class AlertsPage implements ViewWillEnter {
  private readonly client = inject(AlertsClient);
  private readonly removal = inject(AlertRemovalService);
  protected readonly categories = inject(CategoriesService);

  protected readonly list = new RemoteList<AlertDto>(() => this.client.getMine());
  readonly items = this.list.items;
  readonly state = this.list.state;
  /** Alerty, których przełącznik czeka na odpowiedź API. */
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly limitReached = computed(() => this.items().length >= ALERT_RULES.maxPerUser);

  protected readonly maxPerUser = ALERT_RULES.maxPerUser;

  constructor() {
    addIcons({ locationOutline, navigateOutline, notificationsOutline });
  }

  /** Przy każdym wejściu – po zapisie albo usunięciu w edycji lista ma być aktualna. */
  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    this.list.reload();
  }

  /** Podpis: „Hydraulika · 5 km · Dni robocze, 15:00–18:00”. */
  summary(alert: AlertDto): string {
    const category = this.categories.name(alert.categoryId);
    const subcategories = alert.subcategoryIds?.length ?? 0;
    const what = subcategories > 0 ? `${category} (${subcategories})` : category;
    const distance = alert.kind === 'Area' ? formatRadius(alert.radiusKm) : `${formatRadius(alert.radiusKm)} od trasy`;
    return [what, distance, describeSchedule(alert.schedule)].filter((part) => part).join(' · ');
  }

  setEnabled(alert: AlertDto, enabled: boolean): void {
    if (alert.isEnabled === enabled || this.busy().has(alert.id)) {
      return;
    }
    this.mark(alert.id, true);
    this.replace({ ...alert, isEnabled: enabled });
    const request = enabled ? this.client.enable(alert.id) : this.client.disable(alert.id);
    request.subscribe({
      next: () => this.mark(alert.id, false),
      error: () => {
        // Przywracamy przełącznik; komunikat daje interceptor błędów.
        this.replace(alert);
        this.mark(alert.id, false);
      },
    });
  }

  async remove(alert: AlertDto, sliding?: IonItemSliding): Promise<void> {
    await sliding?.close();
    if (await this.removal.remove(alert)) {
      this.items.update((items) => items.filter((item) => item.id !== alert.id));
    }
  }

  private replace(alert: AlertDto): void {
    this.items.update((items) => items.map((item) => (item.id === alert.id ? alert : item)));
  }

  private mark(alertId: string, busy: boolean): void {
    this.busy.update((current) => {
      const next = new Set(current);
      if (busy) {
        next.add(alertId);
      } else {
        next.delete(alertId);
      }
      return next;
    });
  }
}
