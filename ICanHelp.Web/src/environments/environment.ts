// `ng build` (konfiguracja production) podmienia ten plik na environment.prod.ts – angular.json → fileReplacements.
import type { PushEnvironment } from './push-environment';

export const environment: { production: boolean; apiUrl: string; openRouteServiceKey: string } & PushEnvironment = {
  production: false,
  /**
   * Adres API. Pusty = ta sama domena: w dev żądania `/api` i `/hubs` idą przez proxy (`proxy.conf.json`
   * → http://localhost:5080), na produkcji frontend serwuje samo API (Krok F10). Używa go klient API z Kroku F2.
   */
  apiUrl: '',
  // Publiczne dane projektu Firebase – opis w push-environment.ts.
  firebase: {
    apiKey: '',
    authDomain: '',
    projectId: '',
    storageBucket: '',
    messagingSenderId: '',
    appId: '',
  },
  vapidKey: '',
  /**
   * Klucz OpenRouteService (trasy alertów „po drodze”, F8): darmowe konto na openrouteservice.org → Dashboard → Tokens.
   * Klucz jest publiczny (trafia do przeglądarki) – ogranicz go w panelu ORS. Pusty = trasa jako linia prosta start → cel.
   */
  openRouteServiceKey: '',
};
