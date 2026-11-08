// `ng build` (konfiguracja production) podmienia ten plik na environment.prod.ts – angular.json → fileReplacements.
export const environment = {
  production: false,
  /**
   * Adres API. Pusty = ta sama domena: w dev żądania `/api` i `/hubs` idą przez proxy (`proxy.conf.json`
   * → http://localhost:5080), na produkcji frontend serwuje samo API (Krok F10). Używa go klient API z Kroku F2.
   */
  apiUrl: '',
};
