import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { IonButton, IonContent, IonFooter, IonHeader, IonSpinner, IonTitle, IonToolbar } from '@ionic/angular';
import type { ViewWillEnter } from '@ionic/angular';
import { map, switchMap } from 'rxjs';

import { AnnouncementsClient } from '../../core/api/api-client';
import { CategoriesService } from '../../core/categories/categories.service';
import type { GeoPoint } from '../../core/geo/geo-point';
import { ToastService } from '../../core/ui/toast.service';

import type { PhotoItem } from './form/announcement-form';
import { createAnnouncementForm, toCreateCommand } from './form/announcement-form';
import { AnnouncementFormComponent } from './form/announcement-form.component';
import { releasePhotos } from './form/photo-picker.component';
import { syncPhotos } from './form/photo-upload';

/**
 * Nowe ogłoszenie (zakładka „Dodaj”, tylko zalogowani): formularz → `POST api/announcements` → zdjęcia po kolei
 * → szczegóły nowego ogłoszenia. Zakładka zostaje w pamięci, więc po sukcesie czyścimy formularz.
 */
@Component({
  selector: 'app-add-announcement',
  templateUrl: './add-announcement.page.html',
  imports: [IonHeader, IonToolbar, IonTitle, IonContent, IonFooter, IonButton, IonSpinner, AnnouncementFormComponent],
})
export class AddAnnouncementPage implements ViewWillEnter {
  private readonly client = inject(AnnouncementsClient);
  private readonly categories = inject(CategoriesService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly form = createAnnouncementForm(inject(NonNullableFormBuilder));
  readonly location = signal<GeoPoint | null>(null);
  readonly photos = signal<PhotoItem[]>([]);
  readonly submitted = signal(false);
  readonly saving = signal(false);

  ionViewWillEnter(): void {
    this.categories.load().subscribe({ error: () => undefined });
  }

  submit(): void {
    this.submitted.set(true);
    const location = this.location();
    if (this.form.invalid || !location) {
      this.form.markAllAsTouched();
      void this.toast.error('Uzupełnij zaznaczone pola.');
      return;
    }
    if (this.saving()) {
      return;
    }

    const photos = this.photos();
    this.saving.set(true);
    this.client
      .create(toCreateCommand(this.form.getRawValue(), location))
      .pipe(switchMap(({ id }) => syncPhotos(this.client, id, photos).pipe(map((failed) => ({ id, failed })))))
      .subscribe({
        next: ({ id, failed }) => {
          this.saving.set(false);
          void (failed > 0
            ? this.toast.info('Ogłoszenie dodane, ale nie wszystkie zdjęcia – możesz je dodać w edycji.')
            : this.toast.success('Ogłoszenie dodane.'));
          this.reset();
          void this.router.navigate(['/announcements', id]);
        },
        // Komunikat (np. zła kategoria) pokazał interceptor – formularz zostaje, można poprawić i wysłać znowu.
        error: () => this.saving.set(false),
      });
  }

  private reset(): void {
    releasePhotos(this.photos());
    this.photos.set([]);
    this.location.set(null);
    this.submitted.set(false);
    this.form.reset();
  }
}
