import type { ConversationDto } from '../../core/api/api-client';
import type { JobStep } from '../offers/job-step';
import { jobStep } from '../offers/job-step';
import { pendingOffer } from '../offers/offer-labels';

import { jobExitOf, offersOf } from './conversation-timeline';

/**
 * Reguły wątku negocjacji z perspektywy czytającego: co jeszcze może zrobić (oferta, krok zlecenia) i dlaczego nie
 * może już pisać. Czyste funkcje – ekran wątku tylko je wyświetla.
 */

/** Id drugiej strony wątku (do nazwy w nagłówku, licznika i linku do profilu). */
export function otherPartyId(conversation: Pick<ConversationDto, 'myRole' | 'clientId' | 'helperId'>): string {
  return conversation.myRole === 'Client' ? conversation.helperId : conversation.clientId;
}

/** Wątek bez żadnej oferty = pytania i odpowiedzi przed ofertą (oferta to osobny przycisk). */
export function isQuestionsOnly(conversation: ConversationDto): boolean {
  return offersOf(conversation).length === 0;
}

/**
 * Krok realizacji w wątku przyjętego pomocnika (F11 D, `jobStep`): pomocnik – „Rozpocznij” / „Zgłoś wykonanie”,
 * zleceniodawca – „Potwierdź wykonanie” (wyróżnione po zgłoszeniu) i notka, na co czeka. W wątku pomocnika, którego
 * oferty nie przyjęto, i poza realizacją – `null`.
 */
export function threadJobStep(conversation: ConversationDto): JobStep | null {
  if (!offersOf(conversation).some((offer) => offer.status === 'Accepted')) {
    return null;
  }
  const step = jobStep(conversation.announcementStatus, conversation.myRole, conversation.announcementDoneReportedAt);
  return step.action || step.note || step.exit ? step : null;
}

/**
 * Pomocnik w otwartym ogłoszeniu bez oczekującej oferty może złożyć nową (np. po odrzuceniu) – ale nie po zerwanym
 * zleceniu (F11 E: rezygnacja jego albo zleceniodawcy zamyka ten wątek).
 */
export function canMakeOffer(conversation: ConversationDto): boolean {
  return (
    conversation.myRole === 'Helper' &&
    conversation.announcementStatus === 'Open' &&
    !pendingOffer(offersOf(conversation)) &&
    !jobExitOf(conversation)
  );
}

/** Dlaczego nie można pisać (`canWrite: false`) – wg reguły `Announcement.AllowsConversationWith`. */
export function readOnlyReason(conversation: ConversationDto | null): string {
  if (
    conversation &&
    conversation.announcementStatus !== 'Interrupted' &&
    jobExitOf(conversation)?.kind === 'Abandoned'
  ) {
    return 'Zlecenie zerwane przed rozpoczęciem – rozmowa jest tylko do odczytu.';
  }
  switch (conversation?.announcementStatus) {
    case 'Accepted':
    case 'InProgress':
      return 'Zleceniodawca przyjął inną ofertę – rozmowa jest tylko do odczytu.';
    case 'Completed':
      return 'Zlecenie zakończone – rozmowa jest tylko do odczytu. Ocenę wystawisz w Koncie → Oceny.';
    case 'Cancelled':
      return 'Ogłoszenie anulowane – rozmowa jest tylko do odczytu.';
    case 'Interrupted':
      return 'Zlecenie przerwane – rozmowa jest tylko do odczytu. Bez ocen – stanowiska obu stron widać na profilach.';
    default:
      return 'Rozmowa jest tylko do odczytu.';
  }
}

/** Podpowiedź w polu wiadomości: przed ofertą wątek to pytania pomocnika i odpowiedzi zleceniodawcy. */
export function messagePlaceholder(conversation: ConversationDto): string {
  if (!isQuestionsOnly(conversation)) {
    return 'Napisz wiadomość…';
  }
  return conversation.myRole === 'Helper' ? 'Zadaj pytanie…' : 'Odpowiedz…';
}
