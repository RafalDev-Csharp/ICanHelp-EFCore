import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AlertController,
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
  IonSpinner,
  IonTitle,
  IonToggle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { cloudOfflineOutline, locationOutline, navigateOutline, notificationsOutline } from 'ionicons/icons';

import type { AlertDto } from '../../core/api/api-client';
import { AlertsClient } from '../../core/api/api-client';
import { CategoriesService } from '../../core/categories/categories.service';
import { ToastService } from '../../core/ui/toast.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

import { ALERT_RULES, describeSchedule, formatRadius } from './alert-form';

interface CompletableTarget {
  complete(): Promise<void>;
}

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
    IonSpinner,
    EmptyStateComponent,
  ],
})
export class AlertsPage implements ViewWillEnter {
  private readonly client = inject(AlertsClient);
  private readonly dialogs = inject(AlertController);
  private readonly toast = inject(ToastService);
  protected readonly categories = inject(CategoriesService);

  readonly items = signal<AlertDto[]>([]);
  readonly state = signal<'loading' | 'ready' | 'failed'>('loading');
  /** Alerty, których przełącznik czeka na odpowiedź API. */
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly limitReached = computed(() => this.items().length >= ALERT_RULES.maxPerUser);

  protected readonly maxPerUser = ALERT_RULES.maxPerUser;
  protected readonly describeSchedule = describeSchedule;
  protected readonly formatRadius = formatRadius;

  constructor() {
    addIcons({ cloudOfflineOutline, locationOutline, navigateOutline, notificationsOutline });
  }

  /** Przy każdym wejściu – po zapisie albo usunięciu w edycji lista ma być aktualna. */
  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    this.reload();
  }

  reload(done?: () => void): void {
    if (this.items().length === 0) {
      this.state.set('loading');
    }
    this.client.getMine().subscribe({
      next: (items) => {
        this.items.set(items);
        this.state.set('ready');
        done?.();
      },
      error: () => {
        if (this.items().length === 0) {
          this.state.set('failed');
        }
        done?.();
      },
    });
  }

  refresh(event: Event): void {
    this.reload(() => void (event.target as unknown as CompletableTarget).complete());
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
    const dialog = await this.dialogs.create({
      header: 'Usunąć alert?',
      message: `„${alert.name}” – nie dostaniesz już powiadomień z tego alertu.`,
      buttons: [
        { text: 'Anuluj', role: 'cancel' },
        { text: 'Usuń', role: 'destructive' },
      ],
    });
    await dialog.present();
    const { role } = await dialog.onDidDismiss();
    if (role !== 'destructive') {
      return;
    }
    this.client.delete(alert.id).subscribe(() => {
      this.items.update((items) => items.filter((item) => item.id !== alert.id));
      void this.toast.success('Alert usunięty.');
    });
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
