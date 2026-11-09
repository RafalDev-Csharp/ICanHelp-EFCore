import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IonBadge, IonIcon, IonItem, IonLabel, IonNote, IonThumbnail } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { heart, imageOutline } from 'ionicons/icons';

import type { AnnouncementDto } from '../../../core/api/api-client';
import { apiResourceUrl } from '../../../core/api/api.providers';
import { CategoriesService } from '../../../core/categories/categories.service';
import { DistancePipe } from '../../../shared/pipes/distance.pipe';
import { PricePipe } from '../../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../../shared/pipes/time-ago.pipe';
import { pluralPl } from '../../../shared/text/plural';
import { statusColor, statusLabel } from '../announcement-labels';

/**
 * Ogłoszenie na liście (i w podglądzie na mapie): miniatura, tytuł, budżet, kategoria, odległość, wiek,
 * liczba ofert i serduszko (polubione). `showStatus` – status ogłoszenia (np. „Moje ogłoszenia”).
 */
@Component({
  selector: 'app-announcement-card',
  templateUrl: './announcement-card.component.html',
  styleUrls: ['./announcement-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    IonItem,
    IonThumbnail,
    IonIcon,
    IonLabel,
    IonNote,
    IonBadge,
    PricePipe,
    DistancePipe,
    TimeAgoPipe,
  ],
})
export class AnnouncementCardComponent {
  private readonly categories = inject(CategoriesService);

  readonly announcement = input.required<AnnouncementDto>();
  readonly lines = input<'full' | 'inset' | 'none'>('full');
  readonly liked = input(false);
  readonly showStatus = input(false);

  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;

  protected readonly thumbnailUrl = computed(() => apiResourceUrl(this.announcement().thumbnailUrl));
  protected readonly category = computed(() => {
    const { categoryId, subcategoryId } = this.announcement();
    return [this.categories.name(categoryId), this.categories.name(subcategoryId)].filter(Boolean).join(' · ');
  });

  protected readonly offers = computed(() => {
    const count = this.announcement().helperCount;
    return count > 0 ? `${count} ${pluralPl(count, 'oferta', 'oferty', 'ofert')}` : '';
  });

  constructor() {
    addIcons({ heart, imageOutline });
  }
}
