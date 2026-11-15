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
                  (adresy zdjęć), `errorStatus`, `ApiCommands` (komenda po akcji użytkownika: żądanie → komunikat sukcesu
                  → `true` / `false`); categories/ – `CategoriesService` (raz na sesję, `name(id)`, `path(kat, podkat)`),
                  `category-icons` (kafelki „Szukaj”);
                  geo/ – `GeoPoint`, `GeolocationService` (tylko po kliknięciu, ~100 m), `GeocodingService` (Nominatim, ≥ 1,1 s
                  między zapytaniami, `approximatePoint`), `RoutingService` (OpenRouteService, `prepareRoute`; bez klucza –
                  linia prosta);
                  auth/ – `AuthService` (sygnały `currentUser`, `isAuthenticated`; tokeny w localStorage `icanhelp.auth`, synchronizacja kart,
                  jedno odświeżenie naraz), `authGuard` (+ `loginUrlTree`, `safeReturnUrl`);
                  notifications/ – `NotificationsService` (hub SignalR `/hubs/notifications`, dopóki zalogowany; `unreadCount`,
                  `received$` (z huba), `activity$` (hub + push: `{announcementId, conversationId}`), toast „Pokaż”,
                  `openConversation` = wątek na ekranie), `NotificationHubClient` (jedno połączenie: start z ponawianiem, token),
                  `reloadOnActivity` (otwarty ekran przeładowuje dane po `activity$`), `notification-model` (`PushNotice`,
                  `NotificationActivity`, `isRead`, `asRead`), `notification-texts` (tekst i link z `type` + `parameters`),
                  `NOTIFICATION_HUB_FACTORY` (w specs – fałszywe połączenie);
                  pwa/ – `PushService` (FCM: zgoda po akcji – `suggest()` / `enable()`, `api/me/devices`, `unregister()` przed
                  wylogowaniem, push z workera → `NotificationsService.showPush`), `push-platform` (`PUSH_PLATFORM` – dostęp do
                  przeglądarki), `push-storage` (pamięć urządzenia), `push-messages` (`parseWorkerMessage`), `PUSH_MESSAGING`
                  (Firebase SDK, leniwie; w specs zaślepka), `InstallPromptService` (`beforeinstallprompt`, instrukcja iPhone);
                  interceptors/ – `authInterceptor` (Bearer, odświeżenie przed wygaśnięciem i raz po 401), `errorInterceptor`
                  (ProblemDetails → toast, mapa `error-messages.ts`, `SKIP_ERROR_TOAST`); ui/ – `ToastService` (error/success/info),
                  `DialogService` (`confirm`, `prompt` → gotowa odpowiedź), `ModalService` (okno z komponentem → dane albo `null`);
                  testing/ – helpery do specs (`provideNotificationsStub` – strony z dzwonkiem)
shared/           components/ (EmptyStateComponent, `app-loading` / `app-load-failed` – stany „wczytuję” i „nie udało się”
                  z „Spróbuj ponownie”, `app-brand` – logo w nagłówku każdej strony → `/announcements`,
                  `app-notifications-bell` – dzwonek z plakietką w nagłówku korzeni zakładek, `app-map` – Leaflet: pinezki, okrąg promienia, trasa z pasem, tryb poglądowy,
                  `app-place-search` – moja pozycja + adres),
                  data/ (`RemoteList` / `PagedList` – lista z API ze stanem `loading | ready | failed`, odświeżanie i
                  doładowywanie; `RemoteData` / `RemoteItem` – jeden zasób, 404 → `not-found`; `runExclusive` – jedna akcja
                  naraz; `completeIonEvent`), forms/ (`validators`: `notBlank`, `cents`, `minLengthIfPresent`),
                  pipes/ (`price`, `distance` – km z API, `timeAgo` – „2 h temu”), text/ (`pluralPl` – „1 ocena / 2 oceny / 5 ocen”,
                  `rating-texts`, `announcement-fields` – nazwy pól ogłoszenia, `capitalize`)
features/<obszar>/ strony i serwisy obszaru: auth (`/login`, `/register` – poza zakładkami, `?returnUrl=`; `sign-up-form.ts` –
                  reguły, formularz i komenda rejestracji),
                  announcements („Szukaj”: fraza, kafelki kategorii, lista ↔ mapa: `list/` – store, filtry, karta;
                  `details/` – `/announcements/:announcementId`: `AnnouncementDetailsStore` (dane + rola czytającego, `providers`
                  strony), sekcje `app-announcement-content` / `-owner` / `-negotiations`, `announcement-footer.ts`
                  (`footerMode` – która stopka), strona = akcje i stopka; `announcement-negotiations.ts` – negocjacje per
                  pomocnik i „Historia ogłoszenia” właściciela;
                  `form/` – formularz ogłoszenia (pola, miejsce, zdjęcia + `resizeImage`, `syncPhotos`; `AnnouncementFormState` –
                  stan trzymany przez stronę: `validated()`, `fill()`, `clear()`), wspólny dla `add-announcement.page` (`/add`,
                  `?copyOf=<id>` – „Wystaw ponownie” po przerwaniu, bez zdjęć) i `edit/` (`/announcements/:id/edit`);
                  `likes/` – `LikesService`; `testing/` – dane i zaślepki do specs),
                  offers (wspólne dla wątku i szczegółów; `OffersService` – negocjacje: okno → potwierdzenie → API → toast;
                  `JobService` – realizacja: krok zlecenia `run`, wyjście `exit`, `respondToExit`, ocena `rate`;
                  `offer-labels` – teksty, `offerStateLabel` (bez „nieaktualna”) i `offerActions` = reguły domeny;
                  `offer-view` (`OfferView` – oferta gotowa do narysowania + przyciski) + `app-offer-card`;
                  `offer-changes` + `OfferChangesModalComponent` – podgląd zmian ogłoszenia było → jest i decyzja autora
                  oferty (`OffersService.reviewChanges`: podtrzymaj / zmień / wycofaj); `job-step` – `jobStep` (reguły kroku:
                  Rozpocznij / Zgłoś wykonanie / Potwierdź wykonanie, wyjście) i `jobStepView` (teksty, ikony, kolory);
                  `job-exit` (powody, `jobExitView` – wpis o rezygnacji / przerwaniu, `reliabilityLabel`) +
                  `JobExitModalComponent`; `offer-form` – formularz oferty / kontroferty, okna `OfferFormModalComponent`
                  i `RatingModalComponent`; `testing/offer-fixtures`),
                  orders (zakładka „Zlecenia” `/conversations`: Zlecam = moje ogłoszenia, Pomagam = moje wątki pomocnika,
                  grupy po statusie – `order-groups.ts`; „Zakończone” zwinięte na dole, wyszarzone),
                  conversations (`/conversations/all` – wszystkie wątki z filtrem roli, `conversation-item` – wiersz wątku,
                  `/conversations/:conversationId` – wątek: wiadomości, oferty z akcjami, zmiany ogłoszenia, bez ofert =
                  „Pytania i odpowiedzi”; `conversation-timeline.ts` – wpisy + `threadNotice` (zobacz zmiany / aktualną ofertę),
                  `conversation-thread.ts` – reguły wątku (`canMakeOffer`, `threadJobStep`, `readOnlyReason`),
                  `app-conversation-subject` – karta ogłoszenia u góry),
                  notifications (`/notifications` – lista, „Oznacz wszystkie”, nowe z huba na górze),
                  profile (zakładka „Konto”, bez logowania: „Zaloguj się”; `liked-announcements` = zakładka „Ulubione” `/liked`),
                  profile/user-profile (`/users/:userId`, publiczny profil F11 G: Pomagam / Zlecam, przerwane zlecenia;
                  `profile-stats.ts` – teksty statystyk),
                  ratings (`/profile/ratings` – „Do wystawienia” · „Jako pomocnik” · „Jako zleceniodawca”, `api/me/ratings`;
                  `rating-groups.ts` – grupowanie po oceniającym, `app-rating-stars`),
                  alerts (`/profile/alerts` – lista, `/profile/alerts/new?kind=Area|Route`, `/profile/alerts/:alertId` – edycja
                  + „Pasujące teraz”; `alert-form.ts` – stan, walidacja, `WeekDays` ↔ maska; `route-picker` – start/cel → trasa;
                  `AlertRemovalService` – usunięcie po potwierdzeniu, wspólne dla listy i edycji)
tabs/             TabsPage – dolny pasek zakładek, rejestruje ikony zakładek (`addIcons`), uruchamia `NotificationsService`
                  i push; stuknięcie zakładki = jej korzeń (`openTabRoot`, nie ostatni ekran). Strony używające `NotificationsService` – w specs obiekt z `received$` (Subject)
```
Trasy `/add`, `/liked`, `/announcements/:id/edit`, `/conversations` (+ `/all`, `/:conversationId`), `/notifications`, `/profile/ratings`, `/profile/alerts` (+ `/new`, `/:alertId`) mają `authGuard`; ogłoszenia (lista, szczegóły), profil `/users/:userId` i konto są publiczne –
akcje wymagające konta same prowadzą na `/login?returnUrl=…`.
Szeroki ekran: nowa strona dostaje `class="page-column"` na `ion-content` (i na treści stopki) – kolumna 720 px (`global.scss`);
w nagłówku `<app-brand>` w `ion-buttons slot="start"` (po `ion-back-button`); korzeń zakładki dostaje też
`<app-notifications-bell>` w `ion-buttons slot="end"` (w specs `provideNotificationsStub()` + `provideHttpClient()`).
Mapy: Leaflet (`import * as L from 'leaflet'`, style w `global.scss`, kolory pinezek – klasy `.map-pin/.map-user/.map-radius`).
W specs stron z mapą podmieniamy `MapComponent` zaślepką (`TestBed.overrideComponent`) – jsdom nie rysuje map; strony z formularzem
ogłoszenia – `AnnouncementFormStubComponent` (`features/announcements/testing/announcement-form.stub.ts`).
Okna (Ionic modal) dostają dane przez `componentProps` → w komponentach okien zwykłe `@Input()`, nie `input()`; w specs
`ModalController` / `AlertController` zastępujemy obiektem z `create()` (wynik `onWillDismiss` / `onDidDismiss` ustawia test;
`DialogService` i `ModalService` zostają prawdziwe).
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
9. Wspólne klocki zamiast kopiowania (R2): lista z API – `RemoteList` / `PagedList` + `<app-loading>` / `<app-load-failed>`;
   ekran jednego zasobu – `RemoteItem`; akcja z przycisku – `runExclusive(busy, …)`; pytanie – `DialogService.confirm`;
   okno z komponentem – `ModalService.open`; komenda API z komunikatem – `ApiCommands.run`; przeładowanie po
   powiadomieniu – `reloadOnActivity`. Duża strona = store (`providers` strony) + sekcje-komponenty + czyste funkcje
   (reguły, teksty – z własnym spec); akcje użytkownika i stopka z przyciskami zostają na stronie.
   `ion-header` zostaje w szablonie strony – przejścia iOS w Ionic szukają go jako bezpośredniego dziecka strony.

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
