import { signal } from '@angular/core';
import type { NonNullableFormBuilder } from '@angular/forms';

import type { AnnouncementDto } from '../../../core/api/api-client';
import type { GeoPoint } from '../../../core/geo/geo-point';

import type { AnnouncementForm, AnnouncementFormValue, PhotoItem } from './announcement-form';
import { createAnnouncementForm, formValueOf } from './announcement-form';
import { releasePhotos } from './photo-picker.component';

/**
 * Stan formularza ogłoszenia, który trzyma strona („Dodaj” i „Edytuj” – ten sam): pola, miejsce, zdjęcia oraz
 * znaczniki „próbowano wysłać” i „trwa zapis”. Podmiana zdjęć zwalnia podglądy poprzednich (adresy `blob:`).
 */
export class AnnouncementFormState {
  readonly form: AnnouncementForm;
  readonly location = signal<GeoPoint | null>(null);
  readonly photos = signal<PhotoItem[]>([]);
  /** Strona próbowała wysłać – formularz pokazuje błędy także pól, których nikt nie dotknął (miejsce). */
  readonly submitted = signal(false);
  readonly saving = signal(false);

  constructor(fb: NonNullableFormBuilder) {
    this.form = createAnnouncementForm(fb);
  }

  /**
   * Próba wysłania: wartości pól i miejsce albo `null`, gdy czegoś brakuje (pola zostają oznaczone jako błędne –
   * strona pokazuje wtedy komunikat).
   */
  validated(): { value: AnnouncementFormValue; location: GeoPoint } | null {
    this.submitted.set(true);
    const location = this.location();
    if (this.form.invalid || !location) {
      this.form.markAllAsTouched();
      return null;
    }
    return { value: this.form.getRawValue(), location };
  }

  /** Formularz wypełniony ogłoszeniem: edycja (ze zdjęciami) albo kopia do „Wystaw ponownie” (bez zdjęć). */
  fill(announcement: AnnouncementDto, photos: PhotoItem[] = []): void {
    this.setPhotos(photos);
    this.form.reset(formValueOf(announcement));
    this.location.set(announcement.location);
    this.submitted.set(false);
  }

  /** Pusty formularz – po opublikowaniu (zakładka „Dodaj” zostaje w pamięci). */
  clear(): void {
    this.setPhotos([]);
    this.location.set(null);
    this.submitted.set(false);
    this.form.reset();
  }

  /** Zdjęcia już niepotrzebne (zapisane albo porzucone) – zwalnia ich podglądy. */
  releasePhotos(): void {
    releasePhotos(this.photos());
  }

  private setPhotos(photos: PhotoItem[]): void {
    this.releasePhotos();
    this.photos.set(photos);
  }
}
