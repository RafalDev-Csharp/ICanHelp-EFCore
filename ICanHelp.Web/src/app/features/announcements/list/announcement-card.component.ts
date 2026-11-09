import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IonIcon, IonItem, IonLabel, IonNote, IonThumbnail } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { imageOutline } from 'ionicons/icons';

import type { AnnouncementDto } from '../../../core/api/api-client';
import { apiResourceUrl } from '../../../core/api/api.providers';
import { CategoriesService } from '../../../core/categories/categories.service';
import { DistancePipe } from '../../../shared/pipes/distance.pipe';
import { PricePipe } from '../../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../../shared/pipes/time-ago.pipe';

/** Ogłoszenie na liście (i w podglądzie na mapie): miniatura, tytuł, budżet, kategoria, odległość, wiek. */
@Component({
  selector: 'app-announcement-card',
  templateUrl: './announcement-card.component.html',
  styleUrls: ['./announcement-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, IonItem, IonThumbnail, IonIcon, IonLabel, IonNote, PricePipe, DistancePipe, TimeAgoPipe],
})
export class AnnouncementCardComponent {
  private readonly categories = inject(CategoriesService);

  readonly announcement = input.required<AnnouncementDto>();
  readonly lines = input<'full' | 'inset' | 'none'>('full');

  protected readonly thumbnailUrl = computed(() => apiResourceUrl(this.announcement().thumbnailUrl));
  protected readonly category = computed(() => {
    const { categoryId, subcategoryId } = this.announcement();
    return [this.categories.name(categoryId), this.categories.name(subcategoryId)].filter(Boolean).join(' · ');
  });

  constructor() {
    addIcons({ imageOutline });
  }
}
