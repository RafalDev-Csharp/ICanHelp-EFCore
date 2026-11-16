import { PUSH_CLICKED, PUSH_RECEIVED, parseWorkerMessage } from './push-messages';

describe('parseWorkerMessage', () => {
  it('reads a push received while the app is open', () => {
    const message = parseWorkerMessage({
      type: PUSH_RECEIVED,
      title: 'Nowa oferta: 120 zł',
      body: 'Cieknący kran',
      data: { notificationId: 'n1', url: '/conversations/c1', announcementId: 'a1', conversationId: 'c1' },
    });

    expect(message).toEqual({
      kind: 'received',
      notice: {
        notificationId: 'n1',
        title: 'Nowa oferta: 120 zł',
        body: 'Cieknący kran',
        url: '/conversations/c1',
        announcementId: 'a1',
        conversationId: 'c1',
      },
    });
  });

  it('falls back to the app name when the push has no title', () => {
    const message = parseWorkerMessage({ type: PUSH_RECEIVED, data: {} });

    expect(message).toMatchObject({ kind: 'received', notice: { title: 'ICanHelp' } });
  });

  it('reads a click on a system notification', () => {
    expect(parseWorkerMessage({ type: PUSH_CLICKED, url: '/announcements/a1', notificationId: 'n2' })).toEqual({
      kind: 'clicked',
      url: '/announcements/a1',
      notificationId: 'n2',
    });
  });

  it('ignores anything else', () => {
    expect(parseWorkerMessage(null)).toBeNull();
    expect(parseWorkerMessage('text')).toBeNull();
    expect(parseWorkerMessage({ type: 'VERSION_READY' })).toBeNull();
    // Push bez danych – nie ma czego pokazać.
    expect(parseWorkerMessage({ type: PUSH_RECEIVED })).toBeNull();
  });
});
