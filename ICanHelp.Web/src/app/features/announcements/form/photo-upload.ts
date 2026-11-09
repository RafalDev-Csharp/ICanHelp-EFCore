import type { Observable } from 'rxjs';
import { catchError, concat, concatMap, from, map, of, toArray } from 'rxjs';

import type { AnnouncementsClient } from '../../../core/api/api-client';

import type { PhotoItem } from './announcement-form';

/**
 * Zmiany zdjęć po zapisaniu ogłoszenia: najpierw usunięcia (żeby zwolnić miejsce w limicie 5), potem nowe zdjęcia –
 * po kolei, w kolejności z formularza (pierwsze = miniatura). Błąd jednego zdjęcia nie przerywa reszty
 * (komunikat pokazał interceptor); wynik = liczba nieudanych operacji.
 */
export function syncPhotos(
  client: AnnouncementsClient,
  announcementId: string,
  photos: readonly PhotoItem[],
  removedImageIds: readonly string[] = [],
): Observable<number> {
  const removals = from(removedImageIds).pipe(
    concatMap((imageId) => attempt(client.deleteImage(announcementId, imageId))),
  );
  const uploads = from(photos.filter((photo) => photo.kind === 'new')).pipe(
    concatMap((photo) => attempt(client.postImage(announcementId, { data: photo.blob, fileName: photo.fileName }))),
  );
  return concat(removals, uploads).pipe(
    toArray(),
    map((results) => results.filter((ok) => !ok).length),
  );
}

/** Id zapisanych zdjęć, które użytkownik usunął z formularza. */
export function removedImageIds(originalIds: readonly string[], photos: readonly PhotoItem[]): string[] {
  const kept = new Set(photos.filter((photo) => photo.kind === 'existing').map((photo) => photo.id));
  return originalIds.filter((id) => !kept.has(id));
}

function attempt(request: Observable<unknown>): Observable<boolean> {
  return request.pipe(
    map(() => true),
    catchError(() => of(false)),
  );
}
