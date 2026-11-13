# ICanHelp.Web – frontend (kontekst dla asystenta AI)

> Czytaj razem z głównym `../CLAUDE.md` (domena, API, zasady backendu). Plan kroków: `../docs/PLAN-FRONTEND.md`.
> Trzymaj plik krótki – stan prac prowadzimy w głównym CLAUDE.md i w planie.

## Stack
- Angular 22 (standalone, sygnały, `inject()`, lazy loading tras), Ionic 9, TypeScript 6 strict.
- Wygenerowany `ionic start icanhelp-web tabs --type=angular-standalone` (8.11.2026). Komponenty Ionic importujemy z `@ionic/angular`.
- Testy: jednostkowe Vitest (`*.spec.ts` obok kodu, `describe/it/expect` globalnie), e2e Playwright (`e2e/`, telefon Pixel 7).
- Lint: ESLint (`@ionic/eslint-config` + `angular-eslint`) + Prettier (`@ionic/prettier-config`: 120 znaków, `'`, przecinki na końcu).
- Capacitor jest w zależnościach z szablonu, ale platformy (android/ios) dochodzą dopiero w Kroku 17.

## Struktura `src/app/`
```
app.config.ts     providery (router, Ionic, locale) · app.routes.ts → tabs/tabs.routes.ts (zakładki bez prefiksu, F11 B: Szukaj
                  /announcements · Ulubione /liked · Dodaj /add · Zlecenia /conversations · Konto /profile; /notifications
                  bez zakładki – dzwonek; nieznany adres → /announcements)
core/             jedno miejsce na całą aplikację: config/locale.ts (pl-PL, PLN), config/map.ts (kafelki, środek domyślny);
                  api/ – `api-client.ts` (NSwag, generowany), `provideApi()` (API_BASE_URL), `isApiUrl`, `apiResourceUrl`
                  (adresy zdjęć), `errorStatus`; categories/ – `CategoriesService` (raz na sesję, `name(id)`),
                  `category-icons` (kafelki „Szukaj”);
                  geo/ – `GeoPoint`, `GeolocationService` (tylko po kliknięciu, ~100 m), `GeocodingService` (Nominatim, ≥ 1,1 s
                  między zapytaniami, `approximatePoint`), `RoutingService` (OpenRouteService, `prepareRoute`; bez klucza –
                  linia prosta);
                  auth/ – `AuthService` (sygnały `currentUser`, `isAuthenticated`; tokeny w localStorage `icanhelp.auth`, synchronizacja kart,
                  jedno odświeżenie naraz), `authGuard` (+ `loginUrlTree`, `safeReturnUrl`);
                  notifications/ – `NotificationsService` (hub SignalR `/hubs/notifications`, dopóki zalogowany; `unreadCount`,
                  `received$` (z huba), `activity$` (hub + push: `{announcementId, conversationId}` – otwarte ekrany
                  przeładowują dane), toast „Pokaż”, `openConversation` = wątek na ekranie), `notification-texts` (tekst i link
                  z `type` + `parameters`), `NOTIFICATION_HUB_FACTORY` (w specs – fałszywe połączenie);
                  pwa/ – `PushService` (FCM: zgoda po akcji – `suggest()` / `enable()`, `api/me/devices`, `unregister()` przed
                  wylogowaniem, push z workera → `NotificationsService.showPush`), `PUSH_MESSAGING` (Firebase SDK, leniwie;
                  w specs zaślepka), `InstallPromptService` (`beforeinstallprompt`, instrukcja iPhone);
                  interceptors/ – `authInterceptor` (Bearer, odświeżenie przed wygaśnięciem i raz po 401), `errorInterceptor`
                  (ProblemDetails → toast, mapa `error-messages.ts`, `SKIP_ERROR_TOAST`); ui/ – `ToastService` (error/success/info);
                  testing/ – helpery do specs (`provideNotificationsStub` – strony z dzwonkiem)
shared/           components/ (EmptyStateComponent, `app-brand` – logo w nagłówku każdej strony → `/announcements`,
                  `app-notifications-bell` – dzwonek z plakietką w nagłówku korzeni zakładek, `app-map` – Leaflet: pinezki, okrąg promienia, trasa z pasem, tryb poglądowy,
                  `app-place-search` – moja pozycja + adres),
                  pipes/ (`price`, `distance` – km z API, `timeAgo` – „2 h temu”), text/ (`pluralPl` – „1 ocena / 2 oceny / 5 ocen”)
features/<obszar>/ strony i serwisy obszaru: auth (`/login`, `/register` – poza zakładkami, `?returnUrl=`),
                  announcements („Szukaj”: fraza, kafelki kategorii, lista ↔ mapa: `list/` – store, filtry, karta; `details/` – `/announcements/:announcementId`;
                  `form/` – formularz ogłoszenia (pola, miejsce, zdjęcia + `resizeImage`, `syncPhotos`), wspólny dla
                  `add-announcement.page` (`/add`) i `edit/` (`/announcements/:id/edit`); `likes/` – `LikesService`;
                  `details/announcement-negotiations.ts` – negocjacje per pomocnik i „Historia ogłoszenia” właściciela;
                  `testing/` – dane i zaślepki do specs),
                  offers (`offer-labels` – teksty, `offerStateLabel` (bez „nieaktualna”) i `offerActions` = reguły domeny,
                  `offer-changes` + `OfferChangesModalComponent` – podgląd zmian ogłoszenia było → jest i decyzja autora
                  oferty (`OffersService.reviewChanges`: podtrzymaj / zmień / wycofaj), `job-step` – krok realizacji zlecenia
                  (Rozpocznij / Zgłoś wykonanie / Potwierdź wykonanie + notka; wątek i szczegóły), `offer-form` – formularz oferty /
                  kontroferty, okna `OfferFormModalComponent` i `RatingModalComponent`, `OffersService` – okno → potwierdzenie
                  → API → toast, wspólny dla wątku i szczegółów; `testing/offer-fixtures`),
                  orders (zakładka „Zlecenia” `/conversations`: Zlecam = moje ogłoszenia, Pomagam = moje wątki pomocnika,
                  grupy po statusie – `order-groups.ts`; „Zakończone” zwinięte na dole, wyszarzone),
                  conversations (`/conversations/all` – wszystkie wątki z filtrem roli, `conversation-item` – wiersz wątku,
                  `/conversations/:conversationId` – wątek: wiadomości, oferty z akcjami, zmiany ogłoszenia, bez ofert =
                  „Pytania i odpowiedzi”; `conversation-timeline.ts` – wpisy + `threadNotice` (zobacz zmiany / aktualną ofertę)),
                  notifications (`/notifications` – lista, „Oznacz wszystkie”, nowe z huba na górze),
                  profile (zakładka „Konto”, bez logowania: „Zaloguj się”; `liked-announcements` = zakładka „Ulubione” `/liked`;
                  `my-announcements` – bez trasy od F11 B, do usunięcia),
                  ratings (`/profile/ratings` – „Do wystawienia” · „Jako pomocnik” · „Jako zleceniodawca”, `api/me/ratings`),
                  alerts (`/profile/alerts` – lista, `/profile/alerts/new?kind=Area|Route`, `/profile/alerts/:alertId` – edycja
                  + „Pasujące teraz”; `alert-form.ts` – stan, walidacja, `WeekDays` ↔ maska; `route-picker` – start/cel → trasa)
tabs/             TabsPage – dolny pasek zakładek, rejestruje ikony zakładek (`addIcons`), uruchamia `NotificationsService`
                  i push; stuknięcie zakładki = jej korzeń (`openTabRoot`, nie ostatni ekran). Strony używające `NotificationsService` – w specs obiekt z `received$` (Subject)
```
Trasy `/add`, `/liked`, `/announcements/:id/edit`, `/conversations` (+ `/all`, `/:conversationId`), `/notifications`, `/profile/ratings`, `/profile/alerts` (+ `/new`, `/:alertId`) mają `authGuard`; ogłoszenia (lista, szczegóły) i konto są publiczne –
akcje wymagające konta same prowadzą na `/login?returnUrl=…`.
Szeroki ekran: nowa strona dostaje `class="page-column"` na `ion-content` (i na treści stopki) – kolumna 720 px (`global.scss`);
w nagłówku `<app-brand>` w `ion-buttons slot="start"` (po `ion-back-button`); korzeń zakładki dostaje też
`<app-notifications-bell>` w `ion-buttons slot="end"` (w specs `provideNotificationsStub()` + `provideHttpClient()`).
Mapy: Leaflet (`import * as L from 'leaflet'`, style w `global.scss`, kolory pinezek – klasy `.map-pin/.map-user/.map-radius`).
W specs stron z mapą podmieniamy `MapComponent` zaślepką (`TestBed.overrideComponent`) – jsdom nie rysuje map; strony z formularzem
ogłoszenia – `AnnouncementFormStubComponent` (`features/announcements/testing/announcement-form.stub.ts`).
Okna (Ionic modal) dostają dane przez `componentProps` → w komponentach okien zwykłe `@Input()`, nie `input()`; w specs
`ModalController` / `AlertController` zastępujemy obiektem z `create()` (wynik `onWillDismiss` / `onDidDismiss` ustawia test).
`src/environments/environment*.ts`: `apiUrl` – pusty = ta sama domena (dev przez proxy, produkcja: API serwuje frontend, F10);
`firebase` + `vapidKey` – publiczna konfiguracja FCM (puste = push wyłączony); `openRouteServiceKey` – trasy alertów (pusty =
linia prosta).
PWA: jeden service worker `public/app-sw.js` (push + `importScripts('./ngsw-worker.js')`), `ngsw-config.json`, manifest i ikony
w `public/`. W `ng serve` service worker jest wyłączony – PWA i push sprawdzamy na buildzie: `..\serve-local.cmd` (API serwuje
`www/` z `wwwroot`, https://localhost:7080). Panel przeglądarki w aplikacji Claude nie rejestruje service workerów – test w Chrome.

## Zasady (z `docs/PLAN-FRONTEND.md` „Dlaczego tak”)
1. Mobile first – najpierw telefon (Ionic), komputer ma działać.
2. Klient API generowany NSwagiem (`npm run api`, od F2) do `core/api/api-client.ts` – **nie edytujemy** wygenerowanego pliku.
3. Stan w serwisach z sygnałami (`signal`, `computed`), bez NgRx i bez `NgModule`. Zależności przez `inject()`.
4. Błędy API = ProblemDetails z `errorCode` – tłumaczy je jeden interceptor (mapa `errorCode → tekst po polsku`), ekrany nie parsują błędów.
5. Daty z API są w UTC, czasem bez strefy – zawsze przez `parseApiDate` (`shared/pipes/time-ago.pipe.ts`), nie `new Date(str)`.
6. Mapy: Leaflet + OSM; adresy: Nominatim; trasa: OpenRouteService. Pozycja użytkownika tylko po jego akcji, dokładnej nie pokazujemy innym.
7. Interfejs po polsku (`pl-PL`), nazwy w kodzie po angielsku, komentarze mogą być po polsku.
8. Lint wymusza m.in. `import type` dla typów, kolejność importów (zewnętrzne / `../` / `./`, alfabetycznie), typy zwracane
   w publicznych metodach i eksportowanych funkcjach. Nowe strony: `*.page.ts` + `*.page.html`, selektor z prefiksem `app-`.

## Komendy (w folderze `ICanHelp.Web`)
```bash
npm ci                 # instalacja wg package-lock (Node 22 LTS)
npm start              # http://localhost:4200, /api i /hubs → http://localhost:5080 (proxy.conf.json; backend: dotnet run --project ICanHelp.Api)
npm run lint           # ESLint + typecheck e2e + Prettier --check   (npm run lint:fix – poprawia automatycznie)
npm test               # testy jednostkowe (Vitest, jednorazowo)     (npm run test:watch – w trakcie pracy)
npm run test:e2e       # Playwright (pierwszy raz: npx playwright install chromium)
npm run build          # produkcyjny build do www/
npm run api            # regeneracja core/api/api-client.ts z http://localhost:5080/swagger (backend musi działać, wymaga .NET 9)
```
Testy z HTTP: `provideHttpClientTesting()` + `HttpTestingController`; klient NSwag chce odpowiedzi jako Blob → `jsonBlob()` /
`problemBlob()` z `core/testing/api-testing.ts`, a kolejne żądanie (po FileReader) łap przez `await vi.waitFor(() => http.expectOne(...))`.
Test na telefonie: `npm start -- --host 0.0.0.0` w tej samej sieci Wi-Fi, a gdy potrzebny HTTPS (geolokalizacja, PWA, push) –
tunel (Dev Tunnels / ngrok) na port 4200, a dla PWA / pusha – `serve-local.cmd` i tunel na port 5080. CI: job `frontend` w `../.github/workflows/ci.yml` (`npm ci`, lint, test, build).
