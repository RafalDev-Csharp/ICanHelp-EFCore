import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IonBadge, IonButton, IonIcon, IonItem, IonLabel, IonNote, IonThumbnail } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { heart, heartOutline, imageOutline } from 'ionicons/icons';

import type { AnnouncementDto } from '../../../core/api/api-client';
import { apiResourceUrl } from '../../../core/api/api.providers';
import { CategoriesService } from '../../../core/categories/categories.service';
import { DistancePipe } from '../../../shared/pipes/distance.pipe';
import { PricePipe } from '../../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../../shared/pipes/time-ago.pipe';
import { offersCountLabel, statusColor, statusLabel } from '../announcement-labels';

/**
 * Ogłoszenie na liście (i w podglądzie na mapie): miniatura, tytuł, budżet, kategoria, odległość, wiek,
 * liczba ofert i serduszko (polubione). `showStatus` – status ogłoszenia (np. „Moje ogłoszenia”).
 * `likeable` – serduszko jako przycisk (dodaj / usuń z ulubionych → `likeToggle`; obsługę daje strona, np. `LikeToggleService`).
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
    IonButton,
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
  readonly likeable = input(false);
  /** Przełączenie w toku – przycisk serduszka nieaktywny. */
  readonly likePending = input(false);
  readonly likeToggle = output<string>();

  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;

  protected readonly thumbnailUrl = computed(() => apiResourceUrl(this.announcement().thumbnailUrl));
  protected readonly category = computed(() => {
    const { categoryId, subcategoryId } = this.announcement();
    return this.categories.path(categoryId, subcategoryId);
  });

  protected readonly offers = computed(() => {
    const count = this.announcement().helperCount;
    return count > 0 ? offersCountLabel(count) : '';
  });

  constructor() {
    addIcons({ heart, heartOutline, imageOutline });
  }

  /** Serduszko leży w wierszu z linkiem – kliknięcie nie może otworzyć szczegółów. */
  protected toggleLike(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.likeToggle.emit(this.announcement().id);
  }
}
