import { Injectable, inject } from '@angular/core';
import { AlertController, ModalController } from '@ionic/angular';
import type { Observable } from 'rxjs';
import { firstValueFrom } from 'rxjs';

import type { MakeOffer, OfferDto, RateAnnouncement } from '../../core/api/api-client';
import { AnnouncementsClient } from '../../core/api/api-client';
import { PushService } from '../../core/pwa/push.service';
import { ToastService } from '../../core/ui/toast.service';
import { formatPrice } from '../../shared/pipes/price.pipe';

import { OfferFormModalComponent } from './offer-form-modal.component';
import { changedFieldsLabel } from './offer-labels';
import { RatingModalComponent } from './rating-modal.component';

/**
 * Akcje negocjacji wspólne dla wątku rozmowy i szczegółów ogłoszenia: okno oferty / kontroferty / oceny,
 * potwierdzenie nieodwracalnych kroków, wywołanie API, komunikat. Każda metoda zwraca `true`, gdy coś się zmieniło
 * (ekran wtedy przeładowuje dane). Błędy API pokazuje interceptor – tu kończą się `false`.
 */
@Injectable({ providedIn: 'root' })
export class OffersService {
  private readonly client = inject(AnnouncementsClient);
  private readonly modals = inject(ModalController);
  private readonly alerts = inject(AlertController);
  private readonly toast = inject(ToastService);
  private readonly push = inject(PushService);

  /** Pierwsza oferta pomocnika (cena podpowiedziana z budżetu). Zwraca id oferty albo `null`. */
  async make(announcementId: string, suggestedPrice?: number): Promise<string | null> {
    const command = await this.openForm({ heading: 'Twoja oferta', suggestedPrice });
    if (!command) {
      return null;
    }
    const created = await this.run(this.client.postOffer(announcementId, command), 'Oferta wysłana.');
    if (created) {
      // Dobry moment na pytanie o push: czeka na odpowiedź zleceniodawcy.
      void this.push.suggest('Powiadomić Cię, gdy zleceniodawca odpowie na ofertę?');
    }
    return created?.result?.id ?? null;
  }

  /** Kontroferta drugiej strony; dla nieaktualnej oferty – z informacją, co zmieniło się w ogłoszeniu. */
  async counter(offer: OfferDto): Promise<boolean> {
    const changed = changedFieldsLabel(offer.changesSinceOffer);
    const command = await this.openForm({
      heading: 'Kontroferta',
      basis: offer,
      note: offer.isOutdated
        ? `Ogłoszenie zmieniło się od tej oferty${changed ? ` (${changed})` : ''}. Zaproponuj warunki do aktualnej wersji.`
        : undefined,
    });
    if (!command) {
      return false;
    }
    const created = await this.run(
      this.client.postCounterOffer(offer.announcementId, offer.id, command),
      'Kontroferta wysłana.',
    );
    return created !== null;
  }

  async accept(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.confirm(
      'Przyjąć ofertę?',
      `${formatPrice(offer.price.amount)} – pozostałe oferty zostaną odrzucone, a Wy zobaczycie swoje dane kontaktowe.`,
      'Przyjmij',
    );
    return (
      confirmed && (await this.run(this.client.accept(offer.announcementId, offer.id), 'Oferta przyjęta.')) !== null
    );
  }

  async reject(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.confirm('Odrzucić ofertę?', 'Druga strona może potem złożyć nową.', 'Odrzuć', true);
    return (
      confirmed && (await this.run(this.client.reject(offer.announcementId, offer.id), 'Oferta odrzucona.')) !== null
    );
  }

  async withdraw(offer: OfferDto): Promise<boolean> {
    const confirmed = await this.confirm('Wycofać ofertę?', 'Możesz potem złożyć nową.', 'Wycofaj', true);
    return (
      confirmed && (await this.run(this.client.withdraw(offer.announcementId, offer.id), 'Oferta wycofana.')) !== null
    );
  }

  /** Ocena drugiej strony po zakończeniu (raz na ogłoszenie – backend pilnuje, 409 `AlreadyRated`). */
  async rate(announcementId: string, otherParty: string): Promise<boolean> {
    const modal = await this.modals.create({ component: RatingModalComponent, componentProps: { otherParty } });
    await modal.present();
    const { data, role } = await modal.onWillDismiss<RateAnnouncement>();
    if (role !== 'confirm' || !data) {
      return false;
    }
    return (await this.run(this.client.postRating(announcementId, data), 'Dziękujemy za ocenę!')) !== null;
  }

  private async openForm(props: Partial<OfferFormModalComponent>): Promise<MakeOffer | null> {
    const modal = await this.modals.create({ component: OfferFormModalComponent, componentProps: props });
    await modal.present();
    const { data, role } = await modal.onWillDismiss<MakeOffer>();
    return role === 'confirm' && data ? data : null;
  }

  private async confirm(header: string, message: string, action: string, destructive = false): Promise<boolean> {
    const alert = await this.alerts.create({
      header,
      message,
      buttons: [
        { text: 'Nie', role: 'cancel' },
        { text: action, role: destructive ? 'destructive' : 'confirm' },
      ],
    });
    await alert.present();
    const { role } = await alert.onDidDismiss();
    return role === 'confirm' || role === 'destructive';
  }

  /** `{ result }` po sukcesie (także dla `void` z API), `null` przy błędzie (komunikat pokazał interceptor). */
  private async run<T>(request: Observable<T>, success: string): Promise<{ result: T | undefined } | null> {
    try {
      const result = await firstValueFrom(request, { defaultValue: undefined });
      void this.toast.success(success);
      return { result };
    } catch {
      return null;
    }
  }
}
