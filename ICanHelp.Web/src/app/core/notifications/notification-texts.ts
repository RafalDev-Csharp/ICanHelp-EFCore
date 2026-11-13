import { formatDistance } from '../../shared/pipes/distance.pipe';
import { formatPrice } from '../../shared/pipes/price.pipe';
import type { NotificationDto } from '../api/api-client';

/** Tekst powiadomienia do listy i toastu. */
export interface NotificationText {
  title: string;
  body: string;
  /** Nazwa ikony ionicons (zarejestrowana przez `NOTIFICATION_ICONS`). */
  icon: string;
}

/** Pola ogłoszenia (`AnnouncementFields` w backendzie, `changedFields` = „Title, Budget”). */
const FIELD_LABELS: Record<string, string> = {
  Category: 'kategoria',
  Difficulty: 'trudność',
  Title: 'tytuł',
  Description: 'opis',
  Budget: 'budżet',
  Location: 'miejsce',
  Images: 'zdjęcia',
};

/**
 * Tekst powiadomienia. Od Kroku 15 backend zwraca gotowe `title`/`body` (te same co w push –
 * `Application/Notifications/NotificationTextFormatter`), więc je bierzemy; ikona zawsze z `type`.
 * Bez tekstu z API (starsze dane, testy) – składamy go sami z `type` + `parameters`.
 */
export function describeNotification(notification: NotificationDto): NotificationText {
  const fallback = describeFromParameters(notification);
  return notification.title
    ? { title: notification.title, body: notification.body ?? '', icon: fallback.icon }
    : fallback;
}

function describeFromParameters(notification: NotificationDto): NotificationText {
  const p = notification.parameters ?? {};
  const title = p['announcementTitle'] ?? '';
  const price = formatApiPrice(p['price']);

  switch (notification.type) {
    case 'OfferReceived':
      return { title: price ? `Nowa oferta: ${price}` : 'Nowa oferta', body: title, icon: 'pricetag-outline' };
    case 'OfferCountered':
      return { title: price ? `Kontroferta: ${price}` : 'Kontroferta', body: title, icon: 'swap-horizontal-outline' };
    case 'OfferRevised':
      return {
        title: price ? `Oferta zaktualizowana: ${price}` : 'Oferta zaktualizowana',
        body: title,
        icon: 'refresh-outline',
      };
    case 'OfferAccepted':
      return { title: 'Twoja oferta została przyjęta', body: title, icon: 'checkmark-circle-outline' };
    case 'OfferRejected':
      return p['reason'] === 'AnotherOfferAccepted'
        ? { title: 'Zleceniodawca wybrał innego pomocnika', body: title, icon: 'close-circle-outline' }
        : { title: 'Twoja oferta została odrzucona', body: title, icon: 'close-circle-outline' };
    case 'OfferWithdrawn':
      return { title: 'Oferta wycofana', body: title, icon: 'arrow-undo-outline' };
    case 'JobStarted':
      return { title: 'Pomocnik rozpoczął zlecenie', body: title, icon: 'play-outline' };
    case 'JobDoneReported':
      return { title: 'Pomocnik zgłosił wykonanie – potwierdź', body: title, icon: 'flag-outline' };
    case 'JobCompleted':
      return { title: 'Zlecenie zakończone – możesz ocenić', body: title, icon: 'star-outline' };
    case 'JobAbandoned':
      return {
        title: `${actorLabel(p['actorRole'])} zrezygnował ze zlecenia`,
        body: exitBody(title, p['exitReason'], p['note']),
        icon: 'exit-outline',
      };
    case 'JobInterrupted':
      return {
        title: `${actorLabel(p['actorRole'])} przerwał zlecenie`,
        body: exitBody(title, p['exitReason'], p['note']),
        icon: 'stop-circle-outline',
      };
    case 'JobExitResponded': {
      const preview = p['messagePreview'] ?? '';
      return {
        title: `${actorLabel(p['actorRole'])} dodał swoje stanowisko`,
        body: title && preview ? `${title} – ${preview}` : title + preview,
        icon: 'chatbubble-ellipses-outline',
      };
    }
    case 'MessageReceived':
      return {
        title: title ? `Nowa wiadomość: ${title}` : 'Nowa wiadomość',
        body: p['messagePreview'] ?? '',
        icon: 'chatbubble-outline',
      };
    case 'AnnouncementEdited': {
      const fields = changedFields(p['changedFields']);
      return {
        title: title ? `Ogłoszenie zmienione: ${title}` : 'Ogłoszenie zmienione',
        body: fields
          ? `Zmieniono: ${fields}. Sprawdź, czy Twoja oferta jest aktualna.`
          : 'Sprawdź szczegóły ogłoszenia.',
        icon: 'create-outline',
      };
    }
    case 'NewAnnouncementNearby': {
      const distance = formatDistance(parseNumber(p['distanceKm']));
      const where = p['alertKind'] === 'Route' ? 'po drodze' : 'w okolicy';
      const details = [distance, p['categoryName'], p['alertName'] ? `alert „${p['alertName']}”` : '']
        .filter((part) => !!part)
        .join(' · ');
      return {
        title: `Nowe ogłoszenie ${where}`,
        body: details ? `${title} – ${details}` : title,
        icon: 'location-outline',
      };
    }
    case 'NewAnnouncementsDigest':
      return {
        title: `Kolejne nowe ogłoszenia (${p['count'] ?? '…'})`,
        body: 'Pasują do Twoich alertów – zobacz listę ogłoszeń.',
        icon: 'albums-outline',
      };
    default:
      return { title: 'Powiadomienie', body: title, icon: 'notifications-outline' };
  }
}

/**
 * Dokąd prowadzi kliknięcie: oferty i wiadomości – wątek (tam są akcje), edycja i alert – ogłoszenie,
 * skrót alertów – lista ogłoszeń. Bez potrzebnych id – `null` (zostajemy na liście).
 */
export function notificationLink(notification: NotificationDto): string[] | null {
  const announcement = notification.announcementId ? ['/announcements', notification.announcementId] : null;
  switch (notification.type) {
    case 'AnnouncementEdited':
    case 'NewAnnouncementNearby':
      return announcement;
    case 'NewAnnouncementsDigest':
      return ['/announcements'];
    default:
      return notification.conversationId ? ['/conversations', notification.conversationId] : announcement;
  }
}

/** Typy, które dotyczą wątku – nowe takie powiadomienie odświeża otwarty wątek i listę rozmów. */
export function concernsConversation(notification: NotificationDto): boolean {
  return !!notification.conversationId && notification.type !== 'NewAnnouncementNearby';
}

/** `"120.00 PLN"` (`Price.ToString()` w backendzie) → „120 zł”; inna waluta – bez zmian. */
export function formatApiPrice(value: string | undefined): string {
  if (!value) {
    return '';
  }
  const [amount, currency] = value.trim().split(/\s+/);
  const number = parseNumber(amount);
  if (number === null || (currency && currency !== 'PLN')) {
    return value;
  }
  return formatPrice(number);
}

/** F11 E: powody wyjścia ze zlecenia – te same co `JobExitReason` w backendzie (i `features/offers/job-exit.ts`). */
export const JOB_EXIT_REASON_LABELS: Readonly<Record<string, string>> = {
  CannotMakeIt: 'nie mogę w tym terminie',
  TermsDiffer: 'warunki inne niż w ogłoszeniu',
  NoContact: 'brak kontaktu',
  Disagreement: 'konflikt / nie dogadaliśmy się',
  Other: 'inny',
};

function actorLabel(role: string | undefined): string {
  return role === 'Client' ? 'Zleceniodawca' : 'Pomocnik';
}

/** „Cieknący kran – powód: brak kontaktu”; „Inny” → sam opis; inny powód z opisem → „brak kontaktu (opis)”. */
function exitBody(title: string, reason: string | undefined, note: string | undefined): string {
  const label =
    reason === 'Other' && note
      ? note
      : `${JOB_EXIT_REASON_LABELS[reason ?? ''] ?? reason ?? ''}${note ? ` (${note})` : ''}`;
  // F11 G: druga strona może dodać stanowisko (te same teksty co `NotificationTextFormatter`).
  const invite = ' Możesz dodać swoje stanowisko.';
  if (!label) {
    return title + invite;
  }
  const reasonText = label.replace(/\.+$/, '');
  return (title ? `${title} – powód: ${reasonText}.` : `Powód: ${reasonText}.`) + invite;
}

function changedFields(value: string | undefined): string {
  return (value ?? '')
    .split(',')
    .map((field) => field.trim())
    .filter((field) => field && field !== 'None')
    .map((field) => FIELD_LABELS[field] ?? field)
    .join(', ');
}

function parseNumber(value: string | undefined): number | null {
  if (value === undefined || value.trim() === '') {
    return null;
  }
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}
