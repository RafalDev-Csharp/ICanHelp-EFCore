/** Dłuższy bok zdjęcia po zmniejszeniu – wystarczy na ekran telefonu, a plik ma zwykle 200–500 KB. */
export const MAX_IMAGE_SIDE_PX = 1600;
export const JPEG_QUALITY = 0.85;
/** Limit backendu (`ImageFiles.MaxBytes`). */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

/** Błąd z komunikatem po polsku (do toastu). */
export class ImageResizeError extends Error {}

export interface ResizedImage {
  blob: Blob;
  fileName: string;
}

/** Wymiary po zmniejszeniu tak, żeby dłuższy bok miał najwyżej `maxSide` (mniejszych nie powiększamy). */
export function fitWithin(width: number, height: number, maxSide: number): { width: number; height: number } {
  const scale = Math.min(1, maxSide / Math.max(width, height));
  return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}

/** „IMG_1234.HEIC” → „IMG_1234.jpg” (backend i tak rozpoznaje format po zawartości). */
export function jpegFileName(name: string): string {
  const base = name.replace(/\.[^./\\]+$/, '').trim();
  return `${base || 'zdjecie'}.jpg`;
}

/**
 * Zdjęcie z aparatu/galerii → JPEG z dłuższym bokiem ≤ 1600 px (canvas). Na komórkowym internecie wysyłka 300 KB
 * zamiast 5 MB to różnica kilkunastu sekund. `createImageBitmap` obraca zdjęcie wg EXIF, a nowy plik nie ma już
 * metadanych (np. GPS z aparatu) – przy okazji chroni prywatność.
 */
export async function resizeImage(file: File, maxSide = MAX_IMAGE_SIDE_PX): Promise<ResizedImage> {
  if (file.type && !file.type.startsWith('image/')) {
    throw new ImageResizeError('To nie jest zdjęcie.');
  }

  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
  } catch {
    throw new ImageResizeError('Nie udało się odczytać zdjęcia – wybierz plik JPEG, PNG albo WebP.');
  }

  try {
    const size = fitWithin(bitmap.width, bitmap.height, maxSide);
    const canvas = document.createElement('canvas');
    canvas.width = size.width;
    canvas.height = size.height;
    const context = canvas.getContext('2d');
    if (!context) {
      throw new ImageResizeError('Nie udało się przygotować zdjęcia.');
    }
    // JPEG nie ma przezroczystości – tło PNG byłoby czarne.
    context.fillStyle = '#ffffff';
    context.fillRect(0, 0, size.width, size.height);
    context.drawImage(bitmap, 0, 0, size.width, size.height);

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', JPEG_QUALITY));
    if (!blob) {
      throw new ImageResizeError('Nie udało się przygotować zdjęcia.');
    }
    if (blob.size > MAX_IMAGE_BYTES) {
      throw new ImageResizeError('Zdjęcie jest za duże.');
    }
    return { blob, fileName: jpegFileName(file.name) };
  } finally {
    bitmap.close();
  }
}
