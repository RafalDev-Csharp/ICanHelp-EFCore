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
    apiKey: 'AIzaSyCaeiUisElRFnYikgwzPz9hH-MnKJq64t4',
    authDomain: 'icanhelp-u.firebaseapp.com',
    projectId: 'icanhelp-u',
    storageBucket: 'icanhelp-u.firebasestorage.app',
    messagingSenderId: '604489494523',
    appId: '1:604489494523:web:d5325fc86895a700f1a55e',
  },
  vapidKey: 'BPbsgep13c7V_MQ37dOvxOq0J63Mfzu1-0lqxCU2LuOF1jDyWaTmAOh1d9RnBKiHp8feUO1H2ztPLmVm1V6m2eY',
  /**
   * Klucz OpenRouteService (trasy alertów „po drodze”, F8): darmowe konto na openrouteservice.org → Dashboard → Tokens.
   * Klucz jest publiczny (trafia do przeglądarki) – ogranicz go w panelu ORS. Pusty = trasa jako linia prosta start → cel.
   */
  openRouteServiceKey: '',
};
