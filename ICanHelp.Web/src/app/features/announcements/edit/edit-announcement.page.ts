import { Component, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  AlertController,
  IonBackButton,
  IonButton,
  IonButtons,
  IonContent,
  IonFooter,
  IonHeader,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { alertCircleOutline, cloudOfflineOutline, lockClosedOutline } from 'ionicons/icons';
import type { Observable } from 'rxjs';
import { map, of, switchMap } from 'rxjs';

import type { AnnouncementDetailsDto } from '../../../core/api/api-client';
import { AnnouncementsClient } from '../../../core/api/api-client';
import { errorCode, errorStatus } from '../../../core/api/api-errors';
import { apiResourceUrl } from '../../../core/api/api.providers';
import { AuthService } from '../../../core/auth/auth.service';
import { CategoriesService } from '../../../core/categories/categories.service';
import type { GeoPoint } from '../../../core/geo/geo-point';
import { ToastService } from '../../../core/ui/toast.service';
import { BrandComponent } from '../../../shared/components/brand/brand.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import type { PhotoItem } from '../form/announcement-form';
import { createAnnouncementForm, formValueOf, hasChanges, photosOf, toUpdateCommand } from '../form/announcement-form';
import { AnnouncementFormComponent } from '../form/announcement-form.component';
import { releasePhotos } from '../form/photo-picker.component';
import { removedImageIds, syncPhotos } from '../form/photo-upload';

type EditState = 'loading' | 'ready' | 'not-owner' | 'locked' | 'not-found' | 'failed';

/**
 * Edycja ogłoszenia (`/announcements/:id/edit`, tylko właściciel i tylko status Open – jak w domenie).
 * Zapis: `PATCH` ze zmienionymi polami i `expectedRevision` (zawsze – to też kontrola, czy nikt nie zmienił
 * ogłoszenia w międzyczasie), potem zdjęcia. 409 `ConcurrencyConflict` → propozycja wczytania aktualnej wersji.
 */
@Component({
  selector: 'app-edit-announcement',
  templateUrl: './edit-announcement.page.html',
  styles: `
    .edit-warning {
      max-width: 640px;
      margin: 0 auto 16px;
      padding: 12px 16px;
      border-radius: 8px;
      background: rgba(var(--ion-color-warning-rgb), 0.15);
      font-size: 0.875rem;
    }
  `,
  imports: [
    BrandComponent,
    RouterLink,
    IonHeader,
    IonToolbar,
    IonButtons,
    IonBackButton,
    IonTitle,
    IonContent,
    IonFooter,
    IonButton,
    IonSpinner,
    EmptyStateComponent,
    AnnouncementFormComponent,
  ],
})
export class EditAnnouncementPage implements ViewWillEnter {
  private readonly client = inject(AnnouncementsClient);
  private readonly categories = inject(CategoriesService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly alerts = inject(AlertController);

  /** Z adresu `/announcements/:announcementId/edit`. */
  readonly announcementId = input.required<string>();

  readonly form = createAnnouncementForm(inject(NonNullableFormBuilder));
  readonly state = signal<EditState>('loading');
  readonly original = signal<AnnouncementDetailsDto | null>(null);
  readonly location = signal<GeoPoint | null>(null);
  readonly photos = signal<PhotoItem[]>([]);
  readonly submitted = signal(false);
  readonly saving = signal(false);

  protected readonly detailsUrl = computed(() => `/announcements/${this.announcementId()}`);

  constructor() {
    addIcons({ alertCircleOutline, cloudOfflineOutline, lockClosedOutline });
  }

  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
    this.load();
  }

  load(): void {
    this.state.set('loading');
    const user$ = this.auth.currentUser() ? of(this.auth.currentUser()) : this.auth.loadCurrentUser();
    user$
      .pipe(
        switchMap((user) =>
          this.client.getById(this.announcementId()).pipe(map((announcement) => ({ user, announcement }))),
        ),
      )
      .subscribe({
        next: ({ user, announcement }) => {
          if (announcement.ownerId !== user?.id) {
            this.state.set('not-owner');
          } else if (announcement.status !== 'Open') {
            this.state.set('locked');
          } else {
            this.fill(announcement);
            this.state.set('ready');
          }
        },
        error: (error: unknown) => this.state.set(errorStatus(error) === 404 ? 'not-found' : 'failed'),
      });
  }

  save(): void {
    this.submitted.set(true);
    const original = this.original();
    const location = this.location();
    if (!original || this.form.invalid || !location) {
      this.form.markAllAsTouched();
      void this.toast.error('Uzupełnij zaznaczone pola.');
      return;
    }
    if (this.saving()) {
      return;
    }

    const patch = toUpdateCommand(original, this.form.getRawValue(), location);
    const photos = this.photos();
    const removed = removedImageIds(
      original.images.map((image) => image.id),
      photos,
    );
    const photosChanged = removed.length > 0 || photos.some((photo) => photo.kind === 'new');
    if (!hasChanges(patch) && !photosChanged) {
      void this.toast.info('Nic się nie zmieniło.');
      this.leave();
      return;
    }

    this.saving.set(true);
    // PATCH idzie zawsze (także przy samych zdjęciach): bez zmian pól nie tworzy rewizji, a sprawdza `expectedRevision`.
    this.update(patch)
      .pipe(switchMap(() => syncPhotos(this.client, original.id, photos, removed)))
      .subscribe({
        next: (failed) => {
          this.saving.set(false);
          void (failed > 0
            ? this.toast.info('Zmiany zapisane, ale nie wszystkie zdjęcia – spróbuj jeszcze raz.')
            : this.toast.success('Zmiany zapisane.'));
          releasePhotos(this.photos());
          this.leave();
        },
        error: (error: unknown) => {
          this.saving.set(false);
          if (errorCode(error) === 'ConcurrencyConflict') {
            void this.offerReload();
          }
        },
      });
  }

  private update(patch: ReturnType<typeof toUpdateCommand>): Observable<void> {
    // Klient NSwag typuje pola PATCH jako wymagane – pominięte pola = „bez zmian”, więc wysyłamy tylko zmienione.
    return this.client.update(this.announcementId(), patch as Parameters<AnnouncementsClient['update']>[1]);
  }

  private fill(announcement: AnnouncementDetailsDto): void {
    releasePhotos(this.photos());
    this.original.set(announcement);
    this.form.reset(formValueOf(announcement));
    this.location.set(announcement.location);
    this.photos.set(photosOf(announcement.images, (url) => apiResourceUrl(url) ?? url));
    this.submitted.set(false);
  }

  /** Ktoś (np. ta sama osoba w drugiej karcie) zmienił ogłoszenie – nie nadpisujemy, tylko proponujemy odświeżenie. */
  private async offerReload(): Promise<void> {
    const alert = await this.alerts.create({
      header: 'Ogłoszenie się zmieniło',
      message:
        'Ktoś zmienił je w międzyczasie (np. Ty w innej karcie). Wczytaj aktualną wersję i wprowadź zmiany jeszcze raz.',
      buttons: [
        { text: 'Zostań', role: 'cancel' },
        { text: 'Wczytaj aktualną', handler: () => this.load() },
      ],
    });
    await alert.present();
  }

  private leave(): void {
    void this.router.navigateByUrl(this.detailsUrl(), { replaceUrl: true });
  }
}
