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
import { earthOutline, locationOutline, navigateOutline, notificationsOutline } from 'ionicons/icons';

import type { AlertDto, AlertKind } from '../../core/api/api-client';
import { AlertsClient } from '../../core/api/api-client';
import { CategoriesService } from '../../core/categories/categories.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { RemoteList } from '../../shared/data/remote-list';

import { ALERT_RULES, alertWhat, alertWhere, describeSchedule } from './alert-form';
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
  protected readonly kindIcon: Record<AlertKind, string> = {
    Area: 'location-outline',
    Route: 'navigate-outline',
    Country: 'earth-outline',
  };

  constructor() {
    addIcons({ earthOutline, locationOutline, navigateOutline, notificationsOutline });
  }

  /** Przy każdym wejściu – po zapisie albo usunięciu w edycji lista ma być aktualna. */
  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    this.list.reload();
  }

  /** Nagłówek: „Hydraulika”, „Hydraulika (2 podkategorie)”, „Wszystkie kategorie”. */
  what(alert: AlertDto): string {
    return alertWhat(alert.categoryId, alert.subcategoryIds?.length ?? 0, this.categories.name(alert.categoryId));
  }

  /** Podpis: „do 5 km · Dni robocze, 15:00–18:00”, „cała Polska · Codziennie, całą dobę”. */
  summary(alert: AlertDto): string {
    return [alertWhere(alert.kind, alert.radiusKm), describeSchedule(alert.schedule)].join(' · ');
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
