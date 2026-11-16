import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';

import { announcementDetails } from '../testing/announcement-fixtures';

import type { PhotoItem } from './announcement-form';
import { AnnouncementFormState } from './announcement-form-state';

describe('AnnouncementFormState', () => {
  const newPhoto = (id: string): PhotoItem => ({
    kind: 'new',
    id,
    url: `blob:${id}`,
    blob: new Blob(['x']),
    fileName: `${id}.jpg`,
  });

  function create(): AnnouncementFormState {
    return new AnnouncementFormState(TestBed.inject(NonNullableFormBuilder));
  }

  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  });

  it('does not validate an incomplete form and shows all errors', () => {
    const state = create();
    state.form.patchValue({ title: 'Kran' });

    expect(state.validated()).toBeNull();
    expect(state.submitted()).toBe(true);
    expect(state.form.controls.description.touched).toBe(true);
  });

  it('needs a place even when the fields are valid', () => {
    const state = create();
    const announcement = announcementDetails('a1', { title: 'Cieknący kran' });
    state.fill(announcement);
    state.location.set(null);

    expect(state.validated()).toBeNull();

    state.location.set(announcement.location);
    expect(state.validated()).toEqual({
      value: expect.objectContaining({ title: 'Cieknący kran', budget: 100 }),
      location: announcement.location,
    });
  });

  it('fills the form from an announcement and forgets the previous attempt', () => {
    const state = create();
    state.validated();
    const existing: PhotoItem = { kind: 'existing', id: 'i1', url: '/api/x/i1' };

    state.fill(announcementDetails('a1', { title: 'Malowanie płotu' }), [existing]);

    expect(state.form.controls.title.value).toBe('Malowanie płotu');
    expect(state.location()).toEqual({ latitude: 53.84, longitude: 22.98 });
    expect(state.photos()).toEqual([existing]);
    expect(state.submitted()).toBe(false);
  });

  it('releases photo previews when the photos are replaced or the form is cleared', () => {
    const state = create();
    state.photos.set([newPhoto('p1')]);

    state.fill(announcementDetails('a1'));
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:p1');
    expect(state.photos()).toEqual([]);

    state.photos.set([newPhoto('p2')]);
    state.clear();

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:p2');
    expect(state.photos()).toEqual([]);
    expect(state.location()).toBeNull();
    expect(state.form.controls.title.value).toBe('');
  });
});
