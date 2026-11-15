import type { OnInit } from '@angular/core';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonChip,
  IonContent,
  IonHeader,
  IonInput,
  IonLabel,
  IonList,
  IonNote,
  IonRange,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonText,
  IonTitle,
  IonToggle,
  IonToolbar,
} from '@ionic/angular';
import { addIcons } from 'ionicons';
import { notificationsOffOutline } from 'ionicons/icons';
import type { Observable } from 'rxjs';

import type { AlertKind, AnnouncementDto } from '../../core/api/api-client';
import { AlertsClient } from '../../core/api/api-client';
import { errorStatus } from '../../core/api/api-errors';
import { CategoriesService } from '../../core/categories/categories.service';
import { ToastService } from '../../core/ui/toast.service';
import { BrandComponent } from '../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { LoadFailedComponent } from '../../shared/components/load-state/load-failed.component';
import { LoadingComponent } from '../../shared/components/load-state/loading.component';
import { LocationPickerComponent } from '../announcements/form/location-picker.component';
import { AnnouncementCardComponent } from '../announcements/list/announcement-card.component';

import type { AlertFormErrors, AlertFormValue } from './alert-form';
import {
  ALERT_RULES,
  DAYS_ALL,
  DAYS_WEEKEND,
  DAYS_WORKDAYS,
  WEEK_DAYS,
  alertFormFrom,
  emptyAlertForm,
  formatRadius,
  toCreateAlert,
  toUpdateAlert,
  validateAlertForm,
} from './alert-form';
import { AlertRemovalService } from './alert-removal.service';
import { RoutePickerComponent } from './route-picker.component';

/** Ile ogłoszeń pokazuje podgląd „Pasujące teraz”. */
export const PREVIEW_LIMIT = 20;

/** Wartość z pola Ionic (`$event.detail.value` ma typ nieznany) jako tekst; coś innego → pusty. */
function asText(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

/**
 * Nowy alert (`/profile/alerts/new?kind=Area|Route`) albo edycja (`/profile/alerts/:alertId`) – z podglądem
 * „Pasujące teraz” (`/preview`, stan zapisany w API). Rodzaju zapisanego alertu nie da się zmienić (backend też nie pozwala).
 * Stan formularza, walidacja i zamiana na komendy API – `alert-form.ts`; pola wołają `patch` (albo `set…`, gdy
 * wartość z pola trzeba najpierw sprawdzić).
 */
@Component({
  selector: 'app-alert-edit',
  templateUrl: './alert-edit.page.html',
  styleUrls: ['./alert-edit.page.scss'],
  imports: [
    BrandComponent,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonInput,
    IonSelect,
    IonSelectOption,
    IonRange,
    IonToggle,
    IonChip,
    IonLabel,
    IonNote,
    IonText,
    IonButton,
    IonSpinner,
    IonList,
    EmptyStateComponent,
    LoadingComponent,
    LoadFailedComponent,
    AnnouncementCardComponent,
    LocationPickerComponent,
    RoutePickerComponent,
  ],
})
export class AlertEditPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly client = inject(AlertsClient);
  private readonly removal = inject(AlertRemovalService);
  private readonly toast = inject(ToastService);
  protected readonly categories = inject(CategoriesService);

  protected readonly rules = ALERT_RULES;
  protected readonly weekDays = WEEK_DAYS;
  protected readonly presets = [
    { label: 'Codziennie', mask: DAYS_ALL },
    { label: 'Dni robocze', mask: DAYS_WORKDAYS },
    { label: 'Weekend', mask: DAYS_WEEKEND },
  ];
  protected readonly formatRadius = formatRadius;

  /** `null` = nowy alert. */
  readonly alertId = signal<string | null>(null);
  readonly state = signal<'loading' | 'ready' | 'missing' | 'failed'>('loading');
  readonly form = signal<AlertFormValue>(emptyAlertForm('Area'));
  readonly submitted = signal(false);
  readonly saving = signal(false);
  readonly preview = signal<AnnouncementDto[] | null>(null);
  readonly previewFailed = signal(false);

  readonly errors = computed<AlertFormErrors>(() => validateAlertForm(this.form()));
  protected readonly visibleErrors = computed<AlertFormErrors>(() => (this.submitted() ? this.errors() : {}));
  protected readonly subcategories = computed(() => this.categories.subcategoriesOf(this.form().categoryId));
  protected readonly title = computed(() => {
    if (this.alertId()) {
      return 'Alert';
    }
    return this.form().kind === 'Area' ? 'Alert w okolicy' : 'Alert po drodze';
  });

  constructor() {
    addIcons({ notificationsOffOutline });
  }

  ngOnInit(): void {
    this.categories.load().subscribe({ error: () => undefined });
    const alertId = this.route.snapshot.paramMap.get('alertId');
    if (!alertId) {
      const kind: AlertKind = this.route.snapshot.queryParamMap.get('kind') === 'Route' ? 'Route' : 'Area';
      this.form.set(emptyAlertForm(kind));
      this.state.set('ready');
      return;
    }
    this.alertId.set(alertId);
    this.load(alertId);
  }

  load(alertId: string): void {
    this.state.set('loading');
    this.client.getById(alertId).subscribe({
      next: (alert) => {
        this.form.set(alertFormFrom(alert));
        this.state.set('ready');
        this.loadPreview(alertId);
      },
      error: (error: unknown) => this.state.set(errorStatus(error) === 404 ? 'missing' : 'failed'),
    });
  }

  retry(): void {
    const alertId = this.alertId();
    if (alertId) {
      this.load(alertId);
    }
  }

  /** Zmiana pól formularza (miejsce, trasa, dni – wartości już sprawdzone przez komponent pola). */
  patch(changes: Partial<AlertFormValue>): void {
    this.form.update((value) => ({ ...value, ...changes }));
  }

  setName(value: unknown): void {
    this.patch({ name: asText(value) });
  }

  /** Inna kategoria = podkategorie od nowa (należały do poprzedniej). */
  setCategory(value: unknown): void {
    const categoryId = asText(value);
    if (categoryId !== this.form().categoryId) {
      this.patch({ categoryId, subcategoryIds: [] });
    }
  }

  setSubcategories(value: unknown): void {
    this.patch({
      subcategoryIds: Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : [],
    });
  }

  setRadius(value: unknown): void {
    const radiusKm = Number(value);
    if (Number.isFinite(radiusKm)) {
      this.patch({ radiusKm });
    }
  }

  hasDay(bit: number): boolean {
    return (this.form().days & bit) !== 0;
  }

  toggleDay(bit: number): void {
    this.patch({ days: this.form().days ^ bit });
  }

  setLimitHours(value: unknown): void {
    this.patch({ limitHours: value === true });
  }

  setFrom(value: unknown): void {
    this.patch({ from: asText(value) });
  }

  setTo(value: unknown): void {
    this.patch({ to: asText(value) });
  }

  save(): void {
    if (this.saving()) {
      return;
    }
    this.submitted.set(true);
    if (Object.keys(this.errors()).length > 0) {
      void this.toast.error('Uzupełnij zaznaczone pola.');
      return;
    }
    this.saving.set(true);
    const alertId = this.alertId();
    const request: Observable<unknown> = alertId
      ? this.client.update(alertId, toUpdateAlert(this.form()))
      : this.client.create(toCreateAlert(this.form()));
    request.subscribe({
      next: () => {
        this.saving.set(false);
        void this.toast.success(
          alertId ? 'Zapisano zmiany alertu.' : 'Alert zapisany – damy znać o nowych ogłoszeniach.',
        );
        if (alertId) {
          this.loadPreview(alertId);
        } else {
          this.backToList();
        }
      },
      // Komunikat pokazuje interceptor błędów.
      error: () => this.saving.set(false),
    });
  }

  async remove(): Promise<void> {
    const alertId = this.alertId();
    if (alertId && (await this.removal.remove({ id: alertId, name: this.form().name }))) {
      this.backToList();
    }
  }

  loadPreview(alertId: string): void {
    this.previewFailed.set(false);
    this.client.preview(alertId, PREVIEW_LIMIT).subscribe({
      next: (items) => this.preview.set(items),
      error: () => this.previewFailed.set(true),
    });
  }

  private backToList(): void {
    void this.router.navigateByUrl('/profile/alerts', { replaceUrl: true });
  }
}
