import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AnnouncementsClient, CategoriesClient } from '../../core/api/api-client';
import { ToastService } from '../../core/ui/toast.service';

import { AddAnnouncementPage } from './add-announcement.page';
import { AnnouncementFormComponent } from './form/announcement-form.component';
import { AnnouncementFormStubComponent } from './testing/announcement-form.stub';

describe('AddAnnouncementPage', () => {
  const client = { create: vi.fn(), postImage: vi.fn(), deleteImage: vi.fn() };
  const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };

  function create(): AddAnnouncementPage {
    TestBed.configureTestingModule({
      imports: [AddAnnouncementPage],
      providers: [
        provideRouter([]),
        { provide: AnnouncementsClient, useValue: client },
        { provide: CategoriesClient, useValue: { getAll: () => of([]) } },
        { provide: ToastService, useValue: toast },
      ],
    });
    TestBed.overrideComponent(AddAnnouncementPage, {
      remove: { imports: [AnnouncementFormComponent] },
      add: { imports: [AnnouncementFormStubComponent] },
    });
    const fixture = TestBed.createComponent(AddAnnouncementPage);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  function fill(page: AddAnnouncementPage): void {
    page.form.setValue({
      categoryId: 'cat-home',
      subcategoryId: 'sub-plumbing',
      difficulty: 'Medium',
      title: 'Cieknący kran',
      description: 'Kran w kuchni cieknie od tygodnia.',
      budget: 120,
    });
    page.location.set({ latitude: 53.844, longitude: 22.979 });
  }

  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  });

  it('does not send an incomplete announcement', () => {
    const page = create();
    page.form.patchValue({ title: 'Kran' });

    page.submit();

    expect(client.create).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith('Uzupełnij zaznaczone pola.');
    expect(page.form.controls.description.touched).toBe(true);
  });

  it('requires a place on the map', () => {
    const page = create();
    fill(page);
    page.location.set(null);

    page.submit();

    expect(client.create).not.toHaveBeenCalled();
  });

  it('creates the announcement, uploads the photos and opens it', () => {
    client.create.mockReturnValue(of({ id: 'new-1' }));
    client.postImage.mockReturnValue(of({ id: 'img-1' }));
    const page = create();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fill(page);
    const blob = new Blob(['jpeg'], { type: 'image/jpeg' });
    page.photos.set([{ kind: 'new', id: 'p1', url: 'blob:p1', blob, fileName: 'kran.jpg' }]);

    page.submit();

    expect(client.create).toHaveBeenCalledWith({
      categoryId: 'cat-home',
      subcategoryId: 'sub-plumbing',
      difficulty: 'Medium',
      title: 'Cieknący kran',
      description: 'Kran w kuchni cieknie od tygodnia.',
      budget: { amount: 120, currency: 'PLN' },
      location: { latitude: 53.844, longitude: 22.979 },
    });
    expect(client.postImage).toHaveBeenCalledWith('new-1', { data: blob, fileName: 'kran.jpg' });
    expect(toast.success).toHaveBeenCalledWith('Ogłoszenie dodane.');
    expect(navigate).toHaveBeenCalledWith(['/announcements', 'new-1']);
    // Zakładka zostaje w pamięci – formularz jest gotowy na kolejne ogłoszenie.
    expect(page.form.controls.title.value).toBe('');
    expect(page.photos()).toEqual([]);
    expect(page.location()).toBeNull();
  });
});
