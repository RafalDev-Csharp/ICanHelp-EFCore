/**
 * Push przez Firebase Cloud Messaging (Krok F7). Wszystko tu jest publiczne (konfiguracja aplikacji web z konsoli
 * Firebase: Ustawienia projektu → Ogólne → Twoje aplikacje; klucz VAPID: Cloud Messaging → Certyfikaty Web Push).
 * Puste `apiKey` albo `vapidKey` = push wyłączony (aplikacja działa bez niego). Klucz konta serwisowego jest tylko
 * w backendzie (`Push:Fcm:CredentialsPath`).
 */
export interface PushEnvironment {
  firebase: {
    apiKey: string;
    authDomain: string;
    projectId: string;
    storageBucket: string;
    messagingSenderId: string;
    appId: string;
  };
  vapidKey: string;
}
