/*
 * Jedyny service worker aplikacji (Krok F7): powiadomienia push z FCM + Angular `ngsw` (cache „skorupy”, aktualizacje).
 * Rejestruje go `provideServiceWorker('app-sw.js')`, a `PushService` przekazuje tę rejestrację do Firebase
 * `getToken({ serviceWorkerRegistration })` – Firebase nie potrzebuje wtedy własnego `firebase-messaging-sw.js`.
 *
 * Push i kliknięcie obsługujemy sami (bez SDK Firebase w workerze) i PRZED ngsw: `stopImmediatePropagation()` zatrzymuje
 * jego słuchacze, bo ngsw pokazałby systemowy dymek także przy otwartej aplikacji (podwójne powiadomienie).
 * Wiadomość z FCM (HTTP v1, web push): `{ notification: { title, body }, data: { notificationId, type, url, ... } }`.
 */
const PUSH_RECEIVED = 'icanhelp-push-received';
const PUSH_CLICKED = 'icanhelp-push-clicked';

self.addEventListener('push', (event) => {
  event.stopImmediatePropagation();
  const message = readMessage(event);
  if (message) {
    event.waitUntil(onPush(message));
  }
});

self.addEventListener('notificationclick', (event) => {
  event.stopImmediatePropagation();
  event.notification.close();
  event.waitUntil(onClick(event.notification.data || {}));
});

importScripts('./ngsw-worker.js');

function readMessage(event) {
  if (!event.data) {
    return null;
  }
  try {
    const payload = event.data.json();
    const data = payload.data || {};
    const notification = payload.notification || {};
    return { title: notification.title || 'ICanHelp', body: notification.body || '', data };
  } catch {
    return null;
  }
}

async function onPush(message) {
  // Aplikacja jest na ekranie – pokaże toast (NotificationsService), systemowego dymka nie dokładamy.
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const visible = windows.filter((client) => client.visibilityState === 'visible');
  if (visible.length > 0) {
    visible.forEach((client) => client.postMessage({ type: PUSH_RECEIVED, ...message }));
    return;
  }
  await self.registration.showNotification(message.title, {
    body: message.body,
    icon: 'icons/icon-192x192.png',
    badge: 'icons/icon-96x96.png',
    lang: 'pl',
    // To samo powiadomienie (np. ponowna wysyłka) zastępuje poprzednie zamiast się dublować.
    tag: message.data.notificationId || undefined,
    data: message.data,
  });
}

async function onClick(data) {
  const url = appUrl(data.url);
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const client = windows.find((candidate) => new URL(candidate.url).origin === self.location.origin);
  if (client) {
    // Otwarta karta / PWA w tle: przełączamy ekran routerem (bez przeładowania aplikacji).
    await client.focus();
    client.postMessage({ type: PUSH_CLICKED, url: url.pathname + url.search, notificationId: data.notificationId });
    return;
  }
  await self.clients.openWindow(url.href);
}

/** Tylko adresy tej aplikacji – link spoza niej (albo brak) prowadzi na start. */
function appUrl(path) {
  const scope = new URL(self.registration.scope);
  try {
    const url = new URL(path || '', scope);
    return url.origin === scope.origin ? url : scope;
  } catch {
    return scope;
  }
}
