import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { IonBadge, IonButton, IonChip, IonIcon, IonLabel } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { constructOutline, locationOutline, peopleOutline, pricetagOutline } from 'ionicons/icons';

import type { AnnouncementDetailsDto } from '../../../core/api/api-client';
import { apiResourceUrl } from '../../../core/api/api.providers';
import { CategoriesService } from '../../../core/categories/categories.service';
import { openStreetMapUrl } from '../../../core/config/map';
import type { MapMarker } from '../../../shared/components/map/map.component';
import { MapComponent } from '../../../shared/components/map/map.component';
import { PricePipe } from '../../../shared/pipes/price.pipe';
import { TimeAgoPipe } from '../../../shared/pipes/time-ago.pipe';
import { difficultyColor, difficultyLabel, offersCountLabel, statusColor, statusLabel } from '../announcement-labels';

/**
 * Treść ogłoszenia w szczegółach: zdjęcia, status, tytuł, budżet, etykiety (kategoria, trudność, liczba ofert), opis
 * i miejsce na mapie poglądowej. Sama prezentacja – bez akcji.
 */
@Component({
  selector: 'app-announcement-content',
  templateUrl: './announcement-content.component.html',
  styleUrls: ['./announcement-content.component.scss'],
  imports: [IonBadge, IonChip, IonIcon, IonLabel, IonButton, MapComponent, PricePipe, TimeAgoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnnouncementContentComponent {
  private readonly categories = inject(CategoriesService);

  readonly announcement = input.required<AnnouncementDetailsDto>();

  protected readonly difficultyLabel = difficultyLabel;
  protected readonly difficultyColor = difficultyColor;
  protected readonly statusLabel = statusLabel;
  protected readonly statusColor = statusColor;

  protected readonly images = computed(() =>
    this.announcement().images.map((image) => ({ id: image.id, url: apiResourceUrl(image.url) ?? '' })),
  );
  protected readonly category = computed(() => {
    const { categoryId, subcategoryId } = this.announcement();
    return this.categories.path(categoryId, subcategoryId);
  });
  /**
   * Cena z przyjętej oferty – po akceptacji liczy się ona, nie budżet z ogłoszenia (np. 30 zł → umówione 35 zł).
   * Ofertę widzi właściciel i przyjęty pomocnik; obcy dostają budżet.
   */
  protected readonly agreedPrice = computed(() => {
    const { acceptedOfferId, offers } = this.announcement();
    return acceptedOfferId ? offers.find((offer) => offer.id === acceptedOfferId)?.price.amount : undefined;
  });
  /** „Brak ofert” / „1 oferta” / „3 oferty” – etykieta pod tytułem. */
  protected readonly offersLabel = computed(() => {
    const count = this.announcement().helperCount;
    return count === 0 ? 'Brak ofert' : offersCountLabel(count);
  });
  protected readonly marker = computed<MapMarker[]>(() => {
    const announcement = this.announcement();
    return [{ id: announcement.id, ...announcement.location }];
  });
  protected readonly mapLink = computed(() => openStreetMapUrl(this.announcement().location));

  constructor() {
    addIcons({ constructOutline, locationOutline, peopleOutline, pricetagOutline });
  }
}
