import type { NotificationDto } from '../api/api-client';

import { concernsConversation, describeNotification, formatApiPrice, notificationLink } from './notification-texts';

function notification(type: string, parameters: Record<string, string> = {}, overrides = {}): NotificationDto {
  return {
    id: 'n1',
    type,
    title: '',
    body: '',
    url: undefined,
    parameters,
    announcementId: 'a1',
    conversationId: 'c1',
    createdAt: '2026-11-09T10:00:00Z',
    readAt: undefined,
    isRead: false,
    ...overrides,
  };
}

const plain = (text: string): string => text.replace(/\s/g, ' ');

describe('notification texts', () => {
  const title = { announcementTitle: 'Cieknący kran' };

  it('describes offers with the price in złoty', () => {
    const received = describeNotification(notification('OfferReceived', { ...title, price: '120.00 PLN' }));
    expect(plain(received.title)).toBe('Nowa oferta: 120 zł');
    expect(received.body).toBe('Cieknący kran');

    expect(plain(describeNotification(notification('OfferCountered', { ...title, price: '99.50 PLN' })).title)).toBe(
      'Kontroferta: 99,50 zł',
    );
    expect(describeNotification(notification('OfferAccepted', title)).title).toBe('Twoja oferta została przyjęta');
    expect(describeNotification(notification('OfferWithdrawn', title)).title).toBe('Oferta wycofana');

    const revised = describeNotification(notification('OfferRevised', { ...title, price: '180.00 PLN' }));
    expect(plain(revised.title)).toBe('Oferta zaktualizowana: 180 zł');
    expect(revised.icon).toBe('refresh-outline');
  });

  it('prefers the ready text from the API and keeps the icon from the type', () => {
    const text = describeNotification(
      notification('OfferReceived', { ...title, price: '120.00 PLN' }, { title: 'Z serwera', body: 'Treść' }),
    );
    expect(text).toEqual({ title: 'Z serwera', body: 'Treść', icon: 'pricetag-outline' });
  });

  it('tells a rejection apart from choosing another helper', () => {
    expect(describeNotification(notification('OfferRejected', title)).title).toBe('Twoja oferta została odrzucona');
    expect(
      describeNotification(notification('OfferRejected', { ...title, reason: 'AnotherOfferAccepted' })).title,
    ).toBe('Zleceniodawca wybrał innego pomocnika');
  });

  it('describes messages and edits', () => {
    const message = describeNotification(notification('MessageReceived', { ...title, messagePreview: 'Mogę jutro.' }));
    expect(message).toEqual({
      title: 'Nowa wiadomość: Cieknący kran',
      body: 'Mogę jutro.',
      icon: 'chatbubble-outline',
    });

    const edited = describeNotification(
      notification('AnnouncementEdited', { ...title, changedFields: 'Description, Budget', revision: '2' }),
    );
    expect(edited.title).toBe('Ogłoszenie zmienione: Cieknący kran');
    expect(edited.body).toContain('Zmieniono: opis, budżet.');
  });

  it('describes alerts – area, route and the daily digest', () => {
    const nearby = describeNotification(
      notification('NewAnnouncementNearby', {
        ...title,
        categoryName: 'Hydraulika',
        alertName: 'Dom',
        alertKind: 'Area',
        distanceKm: '3.2',
      }),
    );
    expect(nearby.title).toBe('Nowe ogłoszenie w okolicy');
    expect(nearby.body).toBe('Cieknący kran – 3,2 km · Hydraulika · alert „Dom”');

    const route = describeNotification(notification('NewAnnouncementNearby', { ...title, alertKind: 'Route' }));
    expect(route.title).toBe('Nowe ogłoszenie po drodze');
    expect(route.body).toBe('Cieknący kran');

    expect(describeNotification(notification('NewAnnouncementsDigest', { count: '4' })).title).toBe(
      'Kolejne nowe ogłoszenia (4)',
    );
  });

  it('falls back for an unknown type', () => {
    expect(describeNotification(notification('SomethingNew', title))).toEqual({
      title: 'Powiadomienie',
      body: 'Cieknący kran',
      icon: 'notifications-outline',
    });
  });

  it('links to the thread, the announcement or the list', () => {
    expect(notificationLink(notification('OfferReceived'))).toEqual(['/conversations', 'c1']);
    expect(notificationLink(notification('MessageReceived'))).toEqual(['/conversations', 'c1']);
    expect(notificationLink(notification('OfferAccepted', {}, { conversationId: undefined }))).toEqual([
      '/announcements',
      'a1',
    ]);
    expect(notificationLink(notification('AnnouncementEdited'))).toEqual(['/announcements', 'a1']);
    expect(notificationLink(notification('NewAnnouncementNearby', {}, { conversationId: undefined }))).toEqual([
      '/announcements',
      'a1',
    ]);
    expect(notificationLink(notification('NewAnnouncementsDigest', {}, { announcementId: undefined }))).toEqual([
      '/announcements',
    ]);
    expect(
      notificationLink(notification('OfferRejected', {}, { conversationId: undefined, announcementId: undefined })),
    ).toBeNull();
  });

  it('knows which notifications concern a thread', () => {
    expect(concernsConversation(notification('MessageReceived'))).toBe(true);
    expect(concernsConversation(notification('OfferReceived', {}, { conversationId: undefined }))).toBe(false);
    expect(concernsConversation(notification('NewAnnouncementNearby', {}, { conversationId: undefined }))).toBe(false);
  });

  it('formats prices from the API', () => {
    expect(plain(formatApiPrice('150.00 PLN'))).toBe('150 zł');
    expect(formatApiPrice('20.00 EUR')).toBe('20.00 EUR');
    expect(formatApiPrice(undefined)).toBe('');
  });
});
