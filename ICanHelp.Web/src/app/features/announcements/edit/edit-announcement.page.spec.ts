import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AlertController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import type { UserDto } from '../../../core/api/api-client';
import { AnnouncementsClient, CategoriesClient } from '../../../core/api/api-client';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/ui/toast.service';
import { AnnouncementFormComponent } from '../form/announcement-form.component';
import { announcementDetails } from '../testing/announcement-fixtures';
import { AnnouncementFormStubComponent } from '../testing/announcement-form.stub';

import { EditAnnouncementPage } from './edit-announcement.page';

describe('EditAnnouncementPage', () => {
  const client = { getById: vi.fn(), update: vi.fn(), postImage: vi.fn(), deleteImage: vi.fn() };
  const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };
  const alerts = { create: vi.fn(() => Promise.resolve({ present: () => Promise.resolve() })) };
  const auth = {
    currentUser: signal<UserDto | null>({ id: 'owner-1' } as UserDto),
    loadCurrentUser: vi.fn(),
  };

  function create(): { page: EditAnnouncementPage; element: HTMLElement; render: () => Promise<void> } {
    TestBed.configureTestingModule({
      imports: [EditAnnouncementPage],
      providers: [
        provideRouter([]),
        { provide: AnnouncementsClient, useValue: client },
        { provide: CategoriesClient, useValue: { getAll: () => of([]) } },
        { provide: AuthService, useValue: auth },
        { provide: ToastService, useValue: toast },
        { provide: AlertController, useValue: alerts },
      ],
    });
    TestBed.overrideComponent(EditAnnouncementPage, {
      remove: { imports: [AnnouncementFormComponent] },
      add: { imports: [AnnouncementFormStubComponent] },
    });
    const fixture = TestBed.createComponent(EditAnnouncementPage);
    fixture.componentRef.setInput('announcementId', 'a1');
    fixture.componentInstance.ionViewWillEnter();
    fixture.detectChanges();
    return {
      page: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      render: () => fixture.whenStable(),
    };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    auth.currentUser.set({ id: 'owner-1' } as UserDto);
    client.getById.mockReturnValue(
      of(
        announcementDetails('a1', {
          title: 'Cieknący kran',
          revision: 2,
          images: [
            { id: 'i1', announcementId: 'a1', fileName: 'a.jpg', fileExtension: '.jpg', url: '/api/x/i1', addedAt: '' },
          ],
        }),
      ),
    );
  });

  it('fills the form with the announcement', () => {
    const { page } = create();

    expect(page.state()).toBe('ready');
    expect(page.form.controls.title.value).toBe('Cieknący kran');
    expect(page.location()).toEqual({ latitude: 53.84, longitude: 22.98 });
    expect(page.photos()).toEqual([{ kind: 'existing', id: 'i1', url: '/api/x/i1' }]);
  });

  it('saves only the changed fields with the edited revision and removes a deleted photo', () => {
    client.update.mockReturnValue(of(undefined));
    client.deleteImage.mockReturnValue(of(undefined));
    const { page } = create();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    page.form.controls.title.setValue('Cieknący kran w kuchni');
    page.photos.set([]);
    page.save();

    expect(client.update).toHaveBeenCalledWith('a1', { expectedRevision: 2, title: 'Cieknący kran w kuchni' });
    expect(client.deleteImage).toHaveBeenCalledWith('a1', 'i1');
    expect(toast.success).toHaveBeenCalledWith('Zmiany zapisane.');
    expect(navigate).toHaveBeenCalledWith('/announcements/a1', { replaceUrl: true });
  });

  it('offers to reload when someone changed the announcement in the meantime', async () => {
    client.update.mockReturnValue(throwError(() => ({ status: 409, errorCode: 'ConcurrencyConflict' })));
    const { page, render } = create();

    page.form.controls.title.setValue('Nowy tytuł');
    page.save();
    await render();

    expect(alerts.create).toHaveBeenCalledTimes(1);
    expect(page.saving()).toBe(false);
  });

  it('does not let a stranger edit', () => {
    auth.currentUser.set({ id: 'someone-else' } as UserDto);
    const { page } = create();

    expect(page.state()).toBe('not-owner');
  });

  it('does not edit an announcement that is no longer open', async () => {
    client.getById.mockReturnValue(of(announcementDetails('a1', { status: 'Accepted' })));
    const { element, render } = create();
    await render();

    expect(element.querySelector('app-empty-state h2')?.textContent).toContain('nie można już edytować');
    expect(element.querySelector('ion-footer')).toBeNull();
  });
});
