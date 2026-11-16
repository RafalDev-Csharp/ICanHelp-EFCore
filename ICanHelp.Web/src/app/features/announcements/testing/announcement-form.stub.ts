import { Component, input, model } from '@angular/core';

/** Zaślepka `app-announcement-form` dla testów stron – bez mapy (Leaflet w jsdom) i wyboru plików. */
@Component({ selector: 'app-announcement-form', template: '' })
export class AnnouncementFormStubComponent {
  readonly form = input<unknown>();
  readonly location = model<unknown>(null);
  readonly photos = model<unknown[]>([]);
  readonly submitted = input(false);
}
