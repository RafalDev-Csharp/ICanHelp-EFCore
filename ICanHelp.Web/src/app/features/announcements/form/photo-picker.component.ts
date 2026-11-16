import type { OnDestroy } from '@angular/core';
import { ChangeDetectionStrategy, Component, computed, inject, model, signal } from '@angular/core';
import { IonButton, IonIcon, IonNote, IonSpinner } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { cameraOutline, closeCircle, imagesOutline } from 'ionicons/icons';

import { ToastService } from '../../../core/ui/toast.service';

import type { PhotoItem } from './announcement-form';
import { ANNOUNCEMENT_RULES } from './announcement-form';
import { ImageResizeError, resizeImage } from './image-resize';

let nextPhotoId = 0;

/**
 * Zdjęcia ogłoszenia (max 5): „Zrób zdjęcie” (`capture` – na telefonie od razu aparat) albo „Z galerii” (kilka naraz).
 * Nowe zdjęcia zmniejszamy przed wysłaniem (`resizeImage`); wysyła je strona po zapisaniu ogłoszenia.
 */
@Component({
  selector: 'app-photo-picker',
  templateUrl: './photo-picker.component.html',
  styleUrls: ['./photo-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IonButton, IonIcon, IonNote, IonSpinner],
})
export class PhotoPickerComponent implements OnDestroy {
  private readonly toast = inject(ToastService);

  readonly photos = model<PhotoItem[]>([]);

  protected readonly max = ANNOUNCEMENT_RULES.maxImages;
  protected readonly processing = signal(false);
  protected readonly free = computed(() => this.max - this.photos().length);

  constructor() {
    addIcons({ cameraOutline, imagesOutline, closeCircle });
  }

  ngOnDestroy(): void {
    releasePhotos(this.photos());
  }

  async add(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    // Ten sam plik wybrany drugi raz też ma wywołać `change`.
    input.value = '';
    if (files.length === 0) {
      return;
    }

    const accepted = files.slice(0, Math.max(0, this.free()));
    if (accepted.length < files.length) {
      void this.toast.info(`Można dodać najwyżej ${this.max} zdjęć.`);
    }

    this.processing.set(true);
    try {
      for (const file of accepted) {
        try {
          const { blob, fileName } = await resizeImage(file);
          const photo: PhotoItem = {
            kind: 'new',
            id: `new-${++nextPhotoId}`,
            url: URL.createObjectURL(blob),
            blob,
            fileName,
          };
          this.photos.update((photos) => [...photos, photo]);
        } catch (error) {
          void this.toast.error(error instanceof ImageResizeError ? error.message : 'Nie udało się dodać zdjęcia.');
        }
      }
    } finally {
      this.processing.set(false);
    }
  }

  remove(photo: PhotoItem): void {
    releasePhotos([photo]);
    this.photos.update((photos) => photos.filter((item) => item.id !== photo.id));
  }
}

/** Podglądy nowych zdjęć to adresy `blob:` – zwalniamy je, gdy zdjęcie znika z formularza. */
export function releasePhotos(photos: readonly PhotoItem[]): void {
  for (const photo of photos) {
    if (photo.kind === 'new') {
      URL.revokeObjectURL(photo.url);
    }
  }
}
