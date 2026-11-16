import type { OnInit } from '@angular/core';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, model, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import {
  IonInput,
  IonLabel,
  IonNote,
  IonSegment,
  IonSegmentButton,
  IonSelect,
  IonSelectOption,
  IonText,
  IonTextarea,
} from '@ionic/angular';

import { CategoriesService } from '../../../core/categories/categories.service';
import type { GeoPoint } from '../../../core/geo/geo-point';
import { difficultyLabel } from '../announcement-labels';

import type { AnnouncementForm, PhotoItem } from './announcement-form';
import { ANNOUNCEMENT_RULES, DIFFICULTY_LEVELS } from './announcement-form';
import { LocationPickerComponent } from './location-picker.component';
import { PhotoPickerComponent } from './photo-picker.component';

/**
 * Pola ogłoszenia – wspólne dla „Dodaj” i „Edytuj”. Wysyłkę (i to, co dalej) robi strona: ona trzyma formularz,
 * miejsce i zdjęcia, a ten komponent je tylko wyświetla i zmienia.
 */
@Component({
  selector: 'app-announcement-form',
  templateUrl: './announcement-form.component.html',
  styleUrls: ['./announcement-form.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    IonSelect,
    IonSelectOption,
    IonSegment,
    IonSegmentButton,
    IonLabel,
    IonInput,
    IonTextarea,
    IonNote,
    IonText,
    LocationPickerComponent,
    PhotoPickerComponent,
  ],
})
export class AnnouncementFormComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  protected readonly categories = inject(CategoriesService);

  readonly form = input.required<AnnouncementForm>();
  readonly location = model<GeoPoint | null>(null);
  readonly photos = model<PhotoItem[]>([]);
  /** Strona próbowała wysłać – pokazujemy błędy także pól, których nikt nie dotknął (miejsce). */
  readonly submitted = input(false);

  protected readonly rules = ANNOUNCEMENT_RULES;
  protected readonly difficulties = DIFFICULTY_LEVELS;
  protected readonly difficultyLabel = difficultyLabel;
  /** Kategoria wybrana w formularzu – z niej lista podkategorii. */
  protected readonly categoryId = signal('');

  ngOnInit(): void {
    const { categoryId, subcategoryId } = this.form().controls;
    this.categoryId.set(categoryId.value);
    categoryId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) => {
      // Inna kategoria = podkategoria do wybrania od nowa (chyba że ustawiono obie naraz, np. przy wczytaniu edycji).
      if (
        value !== this.categoryId() &&
        !this.categories.subcategoriesOf(value).some((s) => s.id === subcategoryId.value)
      ) {
        subcategoryId.setValue('');
      }
      this.categoryId.set(value);
    });
  }
}
