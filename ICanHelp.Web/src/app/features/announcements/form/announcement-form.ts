import type { AbstractControl, FormControl, FormGroup, NonNullableFormBuilder, ValidationErrors } from '@angular/forms';
import { Validators } from '@angular/forms';

import type {
  AnnouncementDto,
  CreateAnnouncement,
  DifficultyLevel,
  ImageDto,
  UpdateAnnouncement,
} from '../../../core/api/api-client';
import type { GeoPoint } from '../../../core/geo/geo-point';

/** Reguły z backendu (`StringObject`, `Description`, `Price`, `Announcement.MaxImages`); tytuł ≥ 3 – tylko w UI. */
export const ANNOUNCEMENT_RULES = {
  title: { min: 3, max: 100 },
  description: { min: 2, max: 500 },
  /** Budżet w zł: backend wymaga > 0, górna granica chroni przed literówką („15000” zamiast „150,00”). */
  budget: { min: 1, max: 100_000 },
  maxImages: 5,
} as const;

export const DIFFICULTY_LEVELS: readonly DifficultyLevel[] = ['Easy', 'Medium', 'Hard'];

export const DEFAULT_CURRENCY = 'PLN';

export type AnnouncementForm = FormGroup<{
  categoryId: FormControl<string>;
  subcategoryId: FormControl<string>;
  difficulty: FormControl<DifficultyLevel>;
  title: FormControl<string>;
  description: FormControl<string>;
  budget: FormControl<number | null>;
}>;

export type AnnouncementFormValue = ReturnType<AnnouncementForm['getRawValue']>;

/** Zdjęcie w formularzu: już zapisane w ogłoszeniu albo nowe (zmniejszone, czeka na wysłanie). */
export type PhotoItem =
  | { kind: 'existing'; id: string; url: string }
  | { kind: 'new'; id: string; url: string; blob: Blob; fileName: string };

/** Pole nie może być samymi spacjami (backend: `IsNullOrWhiteSpace`). */
function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value && !control.value.trim() ? { blank: true } : null;
}

/** Najwyżej 2 miejsca po przecinku (grosze). */
function cents(control: AbstractControl<number | null>): ValidationErrors | null {
  const value = control.value;
  return value !== null && Math.round(value * 100) !== value * 100 ? { cents: true } : null;
}

export function createAnnouncementForm(fb: NonNullableFormBuilder): AnnouncementForm {
  const { title, description, budget } = ANNOUNCEMENT_RULES;
  return fb.group({
    categoryId: ['', Validators.required],
    subcategoryId: ['', Validators.required],
    difficulty: fb.control<DifficultyLevel>('Easy'),
    title: ['', [Validators.required, notBlank, Validators.minLength(title.min), Validators.maxLength(title.max)]],
    description: [
      '',
      [Validators.required, notBlank, Validators.minLength(description.min), Validators.maxLength(description.max)],
    ],
    budget: fb.control<number | null>(null, [
      Validators.required,
      Validators.min(budget.min),
      Validators.max(budget.max),
      cents,
    ]),
  });
}

/** Wartości formularza z istniejącego ogłoszenia (edycja). */
export function formValueOf(announcement: AnnouncementDto): AnnouncementFormValue {
  return {
    categoryId: announcement.categoryId,
    subcategoryId: announcement.subcategoryId,
    difficulty: toDifficulty(announcement.difficulty),
    title: announcement.title,
    description: announcement.description,
    budget: announcement.budget.amount,
  };
}

export function photosOf(images: readonly ImageDto[], url: (path: string) => string): PhotoItem[] {
  return images.map((image) => ({ kind: 'existing', id: image.id, url: url(image.url) }));
}

export function toCreateCommand(value: AnnouncementFormValue, location: GeoPoint): CreateAnnouncement {
  return {
    categoryId: value.categoryId,
    subcategoryId: value.subcategoryId,
    difficulty: value.difficulty,
    title: value.title.trim(),
    description: value.description.trim(),
    budget: { amount: value.budget ?? 0, currency: DEFAULT_CURRENCY },
    location: { latitude: location.latitude, longitude: location.longitude },
  };
}

/** Ciało PATCH: tylko zmienione pola (pominięte = bez zmian) + rewizja, którą użytkownik edytował. */
export type AnnouncementPatch = Partial<UpdateAnnouncement> & { expectedRevision: number };

export function toUpdateCommand(
  original: AnnouncementDto,
  value: AnnouncementFormValue,
  location: GeoPoint,
): AnnouncementPatch {
  const patch: AnnouncementPatch = { expectedRevision: original.revision };
  const title = value.title.trim();
  const description = value.description.trim();

  // Zmiana kategorii wymaga podania też podkategorii – wysyłamy je zawsze razem.
  if (value.categoryId !== original.categoryId || value.subcategoryId !== original.subcategoryId) {
    patch.categoryId = value.categoryId;
    patch.subcategoryId = value.subcategoryId;
  }
  if (value.difficulty !== original.difficulty) {
    patch.difficulty = value.difficulty;
  }
  if (title !== original.title) {
    patch.title = title;
  }
  if (description !== original.description) {
    patch.description = description;
  }
  if (value.budget !== null && value.budget !== original.budget.amount) {
    patch.budget = { amount: value.budget, currency: original.budget.currency };
  }
  if (!samePoint(location, original.location)) {
    patch.location = { latitude: location.latitude, longitude: location.longitude };
  }
  return patch;
}

/** Czy PATCH coś zmienia (poza samą kontrolą rewizji). */
export function hasChanges(patch: AnnouncementPatch): boolean {
  return Object.keys(patch).some((key) => key !== 'expectedRevision');
}

export function samePoint(a: GeoPoint, b: GeoPoint): boolean {
  const epsilon = 1e-7;
  return Math.abs(a.latitude - b.latitude) < epsilon && Math.abs(a.longitude - b.longitude) < epsilon;
}

function toDifficulty(value: string): DifficultyLevel {
  return (DIFFICULTY_LEVELS as readonly string[]).includes(value) ? (value as DifficultyLevel) : 'Easy';
}
