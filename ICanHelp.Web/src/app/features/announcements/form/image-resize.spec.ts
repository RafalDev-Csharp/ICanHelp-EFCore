import { ImageResizeError, fitWithin, jpegFileName, resizeImage } from './image-resize';

describe('image resizing', () => {
  it('scales the longer side down to the limit and keeps the ratio', () => {
    expect(fitWithin(4000, 3000, 1600)).toEqual({ width: 1600, height: 1200 });
    expect(fitWithin(3000, 4000, 1600)).toEqual({ width: 1200, height: 1600 });
  });

  it('does not enlarge small photos', () => {
    expect(fitWithin(800, 600, 1600)).toEqual({ width: 800, height: 600 });
  });

  it('names the result as JPEG', () => {
    expect(jpegFileName('IMG_1234.HEIC')).toBe('IMG_1234.jpg');
    expect(jpegFileName('kran.w.kuchni.png')).toBe('kran.w.kuchni.jpg');
    expect(jpegFileName('.jpg')).toBe('zdjecie.jpg');
  });

  it('rejects files that are not images', async () => {
    const file = new File(['%PDF'], 'faktura.pdf', { type: 'application/pdf' });

    await expect(resizeImage(file)).rejects.toBeInstanceOf(ImageResizeError);
  });
});
