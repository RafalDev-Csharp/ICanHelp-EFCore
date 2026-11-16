import type { PushEnvironment } from '../../../environments/push-environment';

import { createFirebaseMessaging } from './push-messaging';

const config: PushEnvironment = {
  firebase: {
    apiKey: 'key',
    authDomain: 'demo.firebaseapp.com',
    projectId: 'demo',
    storageBucket: 'demo.appspot.com',
    messagingSenderId: '123',
    appId: '1:123:web:abc',
  },
  vapidKey: 'vapid',
};

// Samego SDK Firebase nie uruchamiamy (w jsdom brak Push API) – sprawdzamy to, co jest nasze.
describe('createFirebaseMessaging', () => {
  it('is configured only with the Firebase web config and the VAPID key', () => {
    expect(createFirebaseMessaging(config).configured).toBe(true);
    expect(createFirebaseMessaging({ ...config, vapidKey: '' }).configured).toBe(false);
    expect(createFirebaseMessaging({ ...config, firebase: { ...config.firebase, apiKey: '' } }).configured).toBe(false);
  });

  it('without a token from this session only unsubscribes from the Push API (no default Firebase worker)', async () => {
    const unsubscribe = vi.fn(() => Promise.resolve(true));
    const registration = {
      pushManager: { getSubscription: () => Promise.resolve({ unsubscribe }) },
    } as unknown as ServiceWorkerRegistration;

    await createFirebaseMessaging(config).deleteToken(registration);

    expect(unsubscribe).toHaveBeenCalled();
  });
});
