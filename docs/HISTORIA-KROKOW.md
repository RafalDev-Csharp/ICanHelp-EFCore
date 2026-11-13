# ICanHelpApp – historia kroków

> Pełny „Stan prac” z `CLAUDE.md` (co zrobiono w każdym kroku, testy, migracje, weryfikacje). W `CLAUDE.md` zostaje skrót.

## Stan prac
- [x] Szkielet: Core, Application (DTO, Queries), Infrastructure
- [x] Przebudowa domeny (Core) – model powyżej
- [x] Krok 1 – Infrastructure + Application dopasowane do nowego Core (konfiguracje EF, repozytoria, query handlery)
- [x] Krok 1b – build zielony, usunięty przestarzały `LocationConfiguration.cs`
- [x] Krok 2 – `ICanHelp.Core.Tests` (xUnit + Shouldly): `Entities/Announcement*Tests.cs`, dane w `AnnouncementTestData.cs` (`Now`, `InStatus(...)`).
- [x] Krok 3 – projekt `ICanHelp.Api`: kontrolery GET dla wszystkich zapytań, Swagger, ProblemDetails (`DomainExceptionHandler`),
  appsettings, launchSettings, `/health`. `dotnet build` zielony u autora (3.11.2026).
- [x] Krok 4 – `docker-compose.yml` (SQL Server 2022), manifest `dotnet-ef`, migracja `Init` (20261105141305), seed kategorii.
- [x] Krok 5 – rejestracja/logowanie (JWT, hash hasła): opis w sekcji „Uwierzytelnianie”, projekt `ICanHelp.Application.Tests`
  (14 testów `SignUpHandler`/`SignInHandler`). Bez nowej migracji (schemat Users bez zmian).
- [x] Krok 6 – komendy ogłoszeń/ofert/oceny (`Commands/AnnouncementCommands.cs`, `OfferCommands.cs`) + endpointy + kontakt po akceptacji.
  Testy: `Core.Tests/.../AnnouncementParticipantsTests.cs`, `Application.Tests/Commands/AnnouncementHandlersTests.cs`. Bez migracji.

- [x] Krok 7 – wyszukiwanie po odległości (opis w sekcji „Infrastructure”), migracja `AnnouncementGeoPoint` (20261105190000,
  pisana ręcznie), testy `Application.Tests/Queries/GetAnnouncementsDistanceFilterTests.cs`.

- [x] Krok 8 – `ICanHelp.Api.Tests` (Testcontainers.MsSql 4.4.0, Mvc.Testing 9.0.11; ~30 testów: auth, prywatność, cały przepływ
  negocjacji, wyszukiwanie po odległości, ProblemDetails), CI `.github/workflows/ci.yml`, README z diagramami Mermaid.
  Zweryfikowane u autora 5.11.2026: `dotnet test` – 172/172 zielone (potwierdza też Kroki 5–7). Push + build w GitHub Actions zielone (autor sprawdzał po każdym pushu; potwierdzone 9.11.2026).

- [x] Krok 9 – PATCH ogłoszenia + rewizje (stare/nowe wartości pól → podświetlanie zmian) + blokada akceptacji nieaktualnej oferty
  (szczegóły: `docs/PLAN-NEGOCJACJE.md`). Migracja `AnnouncementRevisions` (20261106120000, pisana ręcznie). 6.11.2026: build + 195/195 testów zielone.
  Poprawka (różnica netto „było → jest”, `EditsSinceOffer`) – zweryfikowana w pełnym przebiegu Kroku 10.
- [x] Krok 10 – zdjęcia (sekcja „Zdjęcia”), migracja `ImageAddedAt` (20261106150000, ręczna). Testy: `Core.Tests/.../AnnouncementImageTests.cs`,
  `Application.Tests/Commands/ImageHandlersTests.cs`, `Api.Tests/AnnouncementImageTests.cs`. 6.11.2026: build + 241/241 testów zielone
  (w tym integracyjne z migracją; potwierdza też test `LaterEdit_CannotHideEarlierPriceChange` z Kroku 9).
- [x] Krok 11 – wątek negocjacji + prywatność ofert (sekcja „Rozmowa”), migracja `Conversations` (20261106170000, ręczna, z backfillem
  wątków dla istniejących ofert). Testy: `Core.Tests/.../ConversationTests.cs`, `Application.Tests/Commands/ConversationHandlersTests.cs`,
  `Api.Tests/ConversationTests.cs`. Pisane bez kompilatora; zweryfikowane w przebiegu Kroku 12 (286/286).
- [x] Krok 12 – „moje negocjacje”: `GET api/me/conversations` (sekcja „Rozmowa”), bez migracji. Testy: `Api.Tests/MyConversationsTests.cs`,
  `Application.Tests/Queries/MyConversationPreviewTests.cs`. 6.11.2026: build + 286/286 testów zielone
  (w tym integracyjne z migracją `Conversations`), commit 77f0e73.
- [x] Krok 13 – powiadomienia o ofertach/wiadomościach/edycji (sekcja „Powiadomienia” w `docs/ARCHITEKTURA.md`, plan:
  `docs/PLAN-POWIADOMIENIA.md`): zdarzenia domenowe (`Core/Events`, `AggregateRoot`), outbox w `SqlUnitOfWork` + `OutboxProcessor`
  w tle, `Notification` + `TransactionNotificationHandler`, SignalR (`/hubs/notifications`), `api/me/notifications`.
  Migracja `Notifications` (20261108100000, ręczna). Testy: `Core.Tests/.../DomainEventsTests.cs`,
  `Application.Tests/Notifications/TransactionNotificationHandlerTests.cs`, `Api.Tests/NotificationTests.cs`.
  Pisane 8.11.2026 bez kompilatora; tego samego dnia u autora: build + 320/320 testów zielone (w tym integracyjne z migracją).
- [x] Krok 14 – alerty o nowych ogłoszeniach (sekcja „Alerty” w `docs/ARCHITEKTURA.md`, decyzje: `docs/PLAN-POWIADOMIENIA.md`):
  agregat `AnnouncementAlert` (obszar / trasa, kategoria + podkategorie, promień, dni/godziny, włącz/wyłącz, max 10),
  `NewAnnouncementAlertHandler` (`AnnouncementPublished` z outboksa → `NewAnnouncementNearby` / skrót `NewAnnouncementsDigest`,
  limit dzienny, cisza nocna), `api/me/alerts` (CRUD, enable/disable, preview). Migracja `AnnouncementAlerts` (20261108140000, ręczna:
  tabela + trigger `TR_AnnouncementAlerts_Zone` + indeks `IX_AnnouncementAlerts_Zone`). Testy: `Core.Tests/.../AnnouncementAlertTests.cs`,
  `Application.Tests/Commands/AlertHandlersTests.cs`, `Application.Tests/Notifications/NewAnnouncementAlertHandlerTests.cs`,
  `Api.Tests/AlertTests.cs`. Pisane 8.11.2026 bez kompilatora; jedyna poprawka: konflikt nazw `Location` (NTS vs domena) – alias
  `using Location = ICanHelp.Core.ValueObjects.Location;` w plikach z NetTopologySuite. Tego samego dnia: build + 371/371 testów zielone
  (w tym integracyjne z migracją, triggerem i indeksem przestrzennym).
- 8.11.2026 – decyzja: klient najpierw jako PWA (Angular + Ionic), później Capacitor; push przez FCM; „jestem w okolicy”
  na żądanie zamiast śledzenia w tle. Stary Krok 15 rozbity na 15 (push), 16 (dostępność na żądanie), 17 (sklepy, tło).
- [x] Krok F1 (frontend, 8.11.2026) – `ICanHelp.Web/` w tym samym repo (decyzja nr 2 z `docs/PLAN-FRONTEND.md`; rozważany osobny
  folder/repo – zostaje monorepo: jeden commit = endpoint + ekran, NSwag czyta Swaggera z repo, jedno CI). Szablon:
  `ionic start icanhelp-web tabs --type=angular-standalone --no-git --capacitor=false` (Angular 22.2, Ionic 9, TS 6, Vitest, Playwright,
  ESLint 10 + Prettier), folder przemianowany na `ICanHelp.Web`. Zmiany względem szablonu: zakładki Ogłoszenia / Dodaj / Rozmowy /
  Powiadomienia / Profil pod krótkimi adresami (`/announcements`, `/add`, …, bez `/tabs`), puste strony z `EmptyStateComponent`,
  `app.config.ts`, `core/config/locale.ts` (`LOCALE_ID` pl-PL, waluta PLN), pipe'y `price`, `distance`, `timeAgo` (+ `parseApiDate`:
  data z API bez strefy = UTC), `environment.apiUrl`, `proxy.conf.json` (`/api`, `/hubs` z WebSocketami → :5080),
  `npm test` = tylko jednostkowe (bez e2e), `.gitattributes` (LF), `capacitor.config.ts` (appId `pl.icanhelp.app`),
  job `frontend` w CI. Testy: `tabs.page.spec.ts` (atrybuty zakładek – teksty etykiet w jsdom są puste, sprawdza je e2e), `features/feature-pages.spec.ts`, `shared/pipes/pipes.spec.ts`,
  `empty-state.component.spec.ts`, e2e `e2e/app.spec.ts`. Pisane bez `npm` (brak dostępu do rejestru w sesji AI) –
  do potwierdzenia u autora: lint + test + build.
- [x] Krok F2 (frontend + backend, 8.11.2026) – logowanie i rejestracja.
  Backend: agregat `RefreshToken` (hash SHA-256, `Rotate`/`Revoke`, `RevokedAt` = token współbieżności), `IRefreshTokenRepository`,
  `IRefreshTokenService` (Infrastructure: 32 losowe bajty Base64Url), `TokenIssuer`, komendy `RefreshSession` / `EndSession`,
  `SignInHandler` zapisuje refresh token (`IUnitOfWork`), `JwtDto` + `RefreshToken`/`RefreshTokenExpiresAtUtc`, `Auth:RefreshTokenExpiry`
  (30 dni), wyjątek `InvalidRefreshToken` → 401, endpointy `POST api/users/refresh` i `POST api/users/sign-out`, migracja `RefreshTokens`
  (20261108200000, ręczna). Swagger: operationId `Kontroler_Akcja`, `SupportNonNullableReferenceTypes` (pod NSwag).
  Testy: `Core.Tests/.../RefreshTokenTests.cs`, `Application.Tests/Commands/SessionHandlersTests.cs` (+ `SignInHandlerTests`),
  `Api.Tests/SessionTests.cs`.
  Frontend: `nswag.json` + `npm run api` → `core/api/api-client.ts` (Angular, HttpClient, DTO-interfejsy, daty jako string),
  `AuthService`, `authInterceptor`, `errorInterceptor` (+ `error-messages.ts`), `ToastService`, `authGuard`, strony `/login`, `/register`,
  profil (dane z `/me`, wylogowanie). Testy Vitest: auth.service, interceptory, guard, login/register; e2e: przekierowanie na logowanie.
  Pisane bez .NET i bez rejestru npm w sesji AI; jedyna poprawka po uruchomieniu: test logowania (e-mail ze spacjami nie przechodzi
  `Validators.email`). 8.11.2026 u autora: dotnet build + 400/400 testów (186 Core, 136 Application, 78 Api, w tym migracja),
  `npm run api` (NSwag 14.7, wygenerowany klient pasuje do kodu bez zmian), lint + 65/65 testów Vitest + build zielone.
- [x] Krok F3 (frontend, 8.11.2026) – przeglądanie ogłoszeń.
  Zależność: `leaflet` 1.9 + `@types/leaflet` (style w `global.scss`). Nowe: `core/geo` (`GeolocationService` – pozycja tylko po
  kliknięciu, zaokrąglona do ~100 m, komunikaty po polsku), `core/categories/CategoriesService` (raz na sesję, nazwy po id),
  `core/config/map.ts` (kafelki OSM, środek domyślny Augustów, link „Otwórz w mapach”), `apiResourceUrl` (adresy zdjęć z API),
  `ToastService.info`, `shared/components/map` (`app-map`: pinezki, okrąg promienia, pozycja użytkownika, tryb poglądowy),
  `shared/text/plural.ts` (`pluralPl`). Lista (`/announcements`): `AnnouncementListStore` (strony po 20, przewijanie bez końca,
  nowsze żądanie przerywa starsze, bez duplikatów), filtry kategoria/podkategoria/„Blisko mnie” + promień 2–50 km, tylko `Open`,
  karta (`AnnouncementCardComponent`: miniatura, tytuł, budżet, kategoria, odległość, wiek), przełącznik Lista ↔ Mapa (pinezki,
  podgląd wybranego ogłoszenia, „Pokaż więcej”). Szczegóły (`/announcements/:announcementId`, w stosie zakładki): galeria, status,
  budżet, kategoria, trudność, liczba ofert (`helperCount`), opis, mapa, zleceniodawca (`api/users/{id}`) ze średnią i ostatnimi
  opiniami (`api/users/{id}/ratings`), „Złóż ofertę” / „Napisz” (gość → logowanie i powrót; zalogowany – na razie komunikat, F5),
  właściciel widzi „To Twoje ogłoszenie”. Testy Vitest: plural, filtry, store, CategoriesService, GeolocationService, strona listy,
  strona szczegółów (mapa zastąpiona zaślepką).
  Zadania backendowe (zapisane w `docs/PLAN-FRONTEND.md`, F3): `helperCount` w `AnnouncementDto` (lista), endpointy polubień.
  Pisane bez rejestru npm i .NET w sesji AI: zweryfikowane `ngc` (strict templates, Leaflet przez zaślepkę typów), `tsc` specs,
  ESLint i Prettier. 9.11.2026 u autora: `npm install` (lock; npm pominął skrypty instalacyjne narzędzi – esbuild, lmdb itp. –
  bez skutków, binarki są w pakietach opcjonalnych), lint + test + build zielone bez poprawek.
- [x] Krok F4 (frontend + backend, 9.11.2026) – dodawanie i edycja ogłoszenia, polubienia.
  Backend (zadania z F3 + kontrola rewizji): `HelperCount` przeniesione do `AnnouncementDto` (lista, „blisko mnie” i podgląd alertu
  liczą je podzapytaniem `COUNT(DISTINCT HelperId)`), `AnnouncementDetailsDto.IsLiked` (dla zalogowanego czytelnika),
  komendy `LikeAnnouncement` / `UnlikeAnnouncement` (idempotentne, `LikeHandlers.cs`), `PUT` / `DELETE api/announcements/{id}/like`,
  `UpdateAnnouncement.ExpectedRevision` (inna rewizja → `ConcurrencyConflict` 409 – bez tego 409 przy edycji zdarzał się tylko przy
  wyścigu zapisów). Bez migracji (tabela `LikedAnnouncements` była od Kroku 1). Testy: `Application.Tests/Commands/LikeHandlersTests.cs`,
  `UpdateAnnouncementHandlerTests` (+2), `Api.Tests/AnnouncementLikeTests.cs` (polubienia, `helperCount` na liście),
  `AnnouncementEditTests` (+1: nieaktualna rewizja → 409).
  Frontend: `features/announcements/form/` – `createAnnouncementForm` + reguły z backendu, `toCreateCommand`, `toUpdateCommand`
  (PATCH tylko ze zmienionymi polami + `expectedRevision`), `AnnouncementFormComponent` (wspólny dla Dodaj/Edytuj),
  `LocationPickerComponent` („Moja pozycja”, adres z Nominatim – `core/geo/GeocodingService`, ≥ 1,1 s między zapytaniami,
  dotknięcie mapy – nowe `mapClick` w `app-map`; punkt zaokrąglany do ~100 m), `PhotoPickerComponent` (max 5, „Zrób zdjęcie” z `capture`
  i „Z galerii”, `resizeImage`: canvas → JPEG, dłuższy bok 1600 px, bez metadanych EXIF/GPS), `syncPhotos` (usunięcia, potem nowe
  zdjęcia po kolei). Strony: „Dodaj” (`/add` – po sukcesie czyści formularz i otwiera ogłoszenie), „Edytuj”
  (`/announcements/:id/edit` – tylko właściciel i `Open`, 409 `ConcurrencyConflict` → okno „Wczytaj aktualną”), „Moje ogłoszenia”
  (`/profile/announcements`, wszystkie statusy) i „Polubione” (`/profile/liked`). Szczegóły: serduszko w nagłówku, właściciel –
  „Edytuj” (Open) i „Anuluj” (Open/Accepted, z potwierdzeniem). Karta: „N ofert”, serduszko, opcjonalny status. `LikesService`
  (lista polubień raz na sesję, zmiany optymistyczne, czyszczenie po wylogowaniu), `errorCode()` w `api-errors.ts`.
  `api-client.ts` poprawiony ręcznie pod nowe API (backend nie działał w sesji) – **do potwierdzenia `npm run api`**.
  Testy Vitest: formularz (walidacja, komendy), `resizeImage` (wymiary, nazwa, odrzucenie nie-zdjęć), `GeocodingService`,
  `LikesService`, strony Dodaj / Edytuj (formularz zastąpiony zaślepką), szczegóły (+4), lista (+1).
  Pisane bez .NET i bez rejestru npm w sesji AI (ESLint nie ruszył – brak natywnego resolvera dla Linuksa): zweryfikowane `ngc`
  (strict templates), `tsc` specs/e2e i Prettier; kolejność importów sprawdzona ręcznie. Do zrobienia u autora: `dotnet build`,
  `dotnet test`, `npm run api`, `npm run lint`, `npm test`, `npm run build`.
  9.11.2026 lokalnie (`verify.cmd`, Docker): build + dotnet 410/410 (Core 186, Application 142, integracyjne 82), `npm run api`
  (klient przegenerowany, nadpisał ręczne poprawki – kompiluje się), lint + Vitest 116/116 + build zielone bez poprawek.
- [x] Krok F5 (frontend + mały backend, 9.11.2026) – oferty, negocjacje, realizacja, oceny.
  Backend: filtr `announcementId` w `GET api/me/conversations` (`GetMyConversations.AnnouncementId`, warunek w
  `GetMyConversationsHandler`) – właściciel ze szczegółów ogłoszenia trafia do wątku każdego pomocnika (sam `POST …/conversations`
  może tylko pomocnik). Bez migracji. Test integracyjny `MyConversationsTests.Filter_ByAnnouncementId_…`, przykład w `ICanHelp.Api.http`.
  `api-client.ts` (`getMine` z nowym parametrem) poprawiony ręcznie, potem przegenerowany przez `npm run api`.
  Frontend: `features/offers/` – `offer-labels` (statusy, strony, zmienione pola, termin „wt., 10 lis, 17:00–19:00”, `offerActions`:
  przyjąć/odrzucić/kontrować – druga strona, przyjąć tylko aktualną, wycofać – autor, wszystko tylko w `Open`), `offer-form`
  (cena jak budżet, `datetime-local` od–do, walidacja jak w domenie: początek nie w przeszłości, koniec po początku, wiadomość
  2–500), okna `OfferFormModalComponent` (oferta / kontroferta, przy nieaktualnej – co zmieniło się w ogłoszeniu)
  i `RatingModalComponent` (1–5 gwiazdek + komentarz), `OffersService` (okno → potwierdzenie → API → toast).
  `features/conversations/`: zakładka „Rozmowy” (`/conversations`: Wszystkie / Zlecam / Pomagam, ostatnia oferta z oznaczeniem
  „nieaktualna”, podgląd wiadomości, licznik nieprzeczytanych, ostrzeżenie o zmianie ogłoszenia, przewijanie bez końca) i wątek
  (`/conversations/:conversationId`: dymki wiadomości, karty ofert z akcjami wg roli, wpisy zmian ogłoszenia, `markAsRead` przy
  wejściu z nieprzeczytanymi, wysyłanie, „Złóż ofertę” dla pomocnika bez oczekującej oferty, tylko do odczytu z powodem,
  gdy `canWrite = false`; obcy → „Nie ma takiej rozmowy”).
  Szczegóły ogłoszenia: pomocnik – „Złóż ofertę” (okno → oferta → wątek) / „Twoja oferta: X zł” (→ wątek), „Napisz” / „Rozmowa”;
  właściciel – „Oferty i rozmowy” (ostatnia oferta per pomocnik, nieprzeczytane, link do wątku) i „Historia ogłoszenia” (oferty,
  kontroferty, edycje od najnowszych); po akceptacji uczestnicy widzą kontakt (`/contact`: telefon, e-mail), „Rozpocznij”
  (obie strony), „Zakończ” (właściciel), „Anuluj” (właściciel, Accepted), po `Completed` – „Oceń…” (raz; stan z `…/ratings`).
  Szczegóły czekają na `currentUser` przed wczytaniem (rola zależy od użytkownika).
  Testy Vitest: `offer-labels`, `offer-form`, okna, `OffersService`, `conversation-timeline`, strony wątku i listy rozmów,
  `announcement-negotiations`, szczegóły (+5 scenariuszy ról).
  9.11.2026 lokalnie (`verify.cmd`, Docker): build + dotnet 411/411 (Core 186, Application 142, integracyjne 83), `npm run api`
  (klient zgodny z ręczną poprawką), lint + Vitest 157/157 + build zielone.
- [x] Krok F6 (frontend + mały backend, 9.11.2026) – powiadomienia w aplikacji (SignalR).
  Backend: `POST api/me/notifications/read-all?conversationId=` (`MarkAllNotificationsAsRead.ConversationId`, filtr w handlerze) –
  otwarcie wątku gasi jego powiadomienia (inaczej plakietka świeciłaby po przeczytaniu wiadomości w „Rozmowach”). Bez migracji.
  Testy: `MarkNotificationsAsReadHandlerTests` (2), `NotificationTests.MarkAll_WithConversation_MarksOnlyThatThread`, przykład w `.http`.
  Frontend: `@microsoft/signalr`; `core/notifications/` – `notification-hub.ts` (token `NOTIFICATION_HUB_FACTORY`: `HubConnectionBuilder`,
  `accessTokenFactory`, `withAutomaticReconnect` 0/2/5/10/30 s), `NotificationsService` (połączenie, dopóki zalogowany – efekt na
  `isAuthenticated`; pierwsze połączenie ponawiane samo, gdy API nie odpowiada; token odświeżany przed wygaśnięciem; `unreadCount`
  z `unread-count` przy zalogowaniu, po wznowieniu połączenia i powrocie do karty; nowe → licznik +1, `received$`, toast z „Pokaż”
  (nie na liście powiadomień); z otwartego wątku → od razu przeczytane, bez toastu), `notification-texts.ts` (teksty po polsku
  z `type` + `parameters` dla 9 typów, cena „120.00 PLN” → „120 zł”, pola zmian, alert obszar/trasa, `notificationLink`: oferty
  i wiadomości → wątek, edycja i alert → ogłoszenie, skrót → lista). `ToastService.action`. Zakładka „Powiadomienia”: lista
  (nieprzeczytane wyróżnione, przewijanie bez końca, pociągnij-odśwież, „Oznacz wszystkie”, nowe z huba na górze, odnowiony skrót
  przesuwa się), plakietka na zakładce (99+), `TabsPage` uruchamia serwis. Wątek: `openConversation`/`closeConversation`,
  przeładowanie przy powiadomieniu z tego wątku; lista rozmów przeładowuje się przy powiadomieniu o wątku.
  Testy Vitest: teksty, serwis (fałszywe połączenie), strona listy, zakładki, wątek i lista rozmów (+24).
  9.11.2026 lokalnie (`verify.cmd`, Docker): build + dotnet 414/414 (Core 186, Application 144, integracyjne 84), `npm run api`
  (klient zgodny z ręczną poprawką co do działania), lint + Vitest 181/181 + build zielone.
- [x] Krok 15 (backend, 10.11.2026) – kanał push (FCM) i urządzenia użytkownika.
  Core: `UserDevice` (token FCM unikalny, platforma, `LastSeenAt`, `Refresh` – ponowne zgłoszenie / przejęcie tokenu przez innego
  użytkownika, limit 10 – `SelectToRemoveBeforeAdding` wybiera najdawniej widziane), `DevicePlatform`, `InvalidDeviceException` (400),
  `IUserDeviceRepository`.
  Application: `RegisterDevice`/`RemoveDevice` (+ handlery, `DeviceNotFoundException` → 404), `GetMyDevices`/`GetMyDeviceByToken`,
  `DeviceDto` (bez tokenu); `IPushSender` (`PushMessage`, `PushSendResult.InvalidTokens`); `NotificationTextFormatter` (tytuł
  i treść po polsku dla 9 typów + link); `NotificationDto` ma `Title`, `Body`, `Url`; `INotificationChannel` +
  `CompositeNotificationSender` (awaria kanału = log) + `PushNotificationChannel` (nieważne tokeny → usunięcie urządzeń).
  Pakiet `Microsoft.Extensions.Logging.Abstractions`.
  Infrastructure: `UserDeviceConfiguration`, repozytorium, query handlery, `Push/` (`PushOptions` – sekcja `Push`, `FcmPushSender`
  – NuGet `FirebaseAdmin` 3.7.0, `LoggingPushSender`), migracja `UserDevices` (wygenerowana `dotnet ef`, bez GeoPoint).
  Api: `DevicesController` (`api/me/devices`), SignalR jako `INotificationChannel`, `Push` w `appsettings.json`, przykłady w `.http`.
  Testy: Core `UserDeviceTests`, Application `NotificationTextFormatterTests`, `NotificationChannelsTests`, `DeviceHandlersTests`,
  Api `DeviceTests` (7: idempotencja, obcy → 404, przejęcie tokenu, 400, 401, push do właściciela a nie autora, nieważny token usuwa
  urządzenie; `RecordingPushSender` w `ApiFactory`).
  Frontend: `describeNotification` bierze `title`/`body` z API (ikona z `type`), klient przegenerowany (`DevicesClient`).
  10.11.2026 lokalnie (`verify.cmd`, Docker): build + dotnet 468/468 (Core 198, Application 179, integracyjne 91), `npm run api`,
  lint + Vitest 182/182 + build zielone. Ręczny test FCM (konsola Firebase, VAPID, telefony) – w F7.
- [x] Krok F7 (frontend, 10.11.2026) – PWA + push (FCM).
  PWA: `ng add @angular/pwa` (`@angular/service-worker`), `ngsw-config.json` (skorupa prefetch, zasoby lazy, `navigationUrls`
  bez `/api`, `/hubs`, `/swagger`, `/health`), `public/manifest.webmanifest` (pl, `ICanHelp`, standalone, start `/announcements`,
  `#0054e9`), własne ikony 72–512 + maskable + `apple-touch-icon` (+ favicon), `index.html` (theme-color, meta iOS);
  `angular.json` – katalog `public/` w assets.
  Service worker: `public/app-sw.js` (rejestrowany przez `provideServiceWorker('app-sw.js')`) – `push` i `notificationclick`
  przed `importScripts('./ngsw-worker.js')` (ngsw zatrzymany); widoczna aplikacja → `postMessage` zamiast dymka, ukryta → dymek
  (`tag` = notificationId); kliknięcie → focus + `postMessage` z adresem albo `openWindow`, tylko adresy tej aplikacji.
  `core/pwa/`: `push-messaging.ts` (`PUSH_MESSAGING` – Firebase JS SDK 13, leniwy import, `getToken({ vapidKey,
  serviceWorkerRegistration })`, `deleteToken` / wypisanie z Push API), `push.service.ts` (`PushService`: stan `unavailable |
  needs-install | unsupported | default | denied | enabled`, `suggest()` – okno po akcji, raz na tydzień, `enable()` – zgoda +
  `POST api/me/devices`, ponowny zapis przy starcie, `unregister()` przed wylogowaniem, wiadomości z workera → `NotificationsService`;
  `PUSH_PLATFORM` – API przeglądarki), `install-prompt.service.ts` (`beforeinstallprompt`, `appinstalled`, iOS / standalone;
  tworzony w `provideAppInitializer`). `environments/push-environment.ts` + pola `firebase`, `vapidKey` (puste = push wyłączony).
  `NotificationsService.showPush` / `openPush` (odsiewanie duplikatów hub + push po id, licznik bez podwójnego +1).
  Ekrany: profil – sekcja „Aplikacja” (`AppSettingsComponent`: instalacja / instrukcja iPhone, stan push + „Włącz”), wylogowanie
  najpierw wyrejestrowuje urządzenie; `OffersService.make` i dodanie ogłoszenia → `suggest()`; `TabsPage` uruchamia `PushService`.
  `errorInterceptor`: `api/me/devices` bez toastu (zapis w tle).
  Testy Vitest: `PushService` (10, zaślepki Firebase i workera), `InstallPromptService` (5), `createFirebaseMessaging` (2),
  `NotificationsService` push (+5), `AppSettingsComponent` (5), poprawki specs ofert, dodawania i zakładek. `app-sw.js` sprawdzony
  symulacją środowiska workera w Node (bez dymka przy widocznej aplikacji, ngsw nie dubluje, obcy link → start); panel przeglądarki
  w Claude nie rejestruje service workerów, więc test na żywo – ręcznie.
  Do zrobienia ręcznie: konfiguracja Firebase w `environment*.ts`, test na Androidzie i iPhonie przez HTTPS, Lighthouse.
  10.11.2026 lokalnie (`verify.cmd`, Docker): build + dotnet 468/468 (Core 198, Application 179, integracyjne 91), `npm run api`
  (klient bez zmian), lint + Vitest 209/209 + build zielone.
- [x] Krok F8 (frontend, 12.11.2026) – alerty „w okolicy” i „po drodze”.
  `features/alerts/`: `alerts.page` (`/profile/alerts` – lista z rodzajem, kategorią (+ liczbą podkategorii), promieniem
  i harmonogramem, `ion-toggle` → `enable`/`disable` od razu z cofnięciem przy błędzie, przesunięcie → usuń z potwierdzeniem,
  limit 10 – bez przycisków „Nowy”), `alert-edit.page` (`/profile/alerts/new?kind=Area|Route` i `/profile/alerts/:alertId`:
  nazwa, kategoria + podkategorie (`ion-select` multiple, puste = cała), miejsce / trasa, suwak promienia 0,5–50 km co 0,5,
  dni (skróty + Pn–Nd), godziny opcjonalne; zapis `POST` → lista, `PATCH` bez `kind` → toast + nowy podgląd; „Pasujące teraz”
  z `/preview?limit=20` kartami ogłoszeń; 404 → „Nie ma takiego alertu”; usuń z potwierdzeniem), `alert-form.ts` (stan
  formularza, walidacja jak w domenie, `WeekDays` ↔ maska bitów, `TimeOnly` ↔ „HH:mm”, opisy po polsku, `toCreateAlert` /
  `toUpdateAlert` – obszar wysyła tylko `center`, trasa tylko `routePoints`), `route-picker.component` (Start / Cel, adres,
  moja pozycja, dotknięcie mapy, trasa sama po wybraniu obu punktów, zmiana punktu unieważnia trasę, ten sam punkt – błąd).
  `core/geo/routing.service.ts` (`RoutingService.plan` – OpenRouteService `driving-car/geojson`, klucz `OPEN_ROUTE_SERVICE_KEY`
  z `environment.openRouteServiceKey`, pusty = linia prosta; `prepareRoute` – zaokrąglenie, bez powtórzeń, Douglas–Peucker),
  `ROUTING` w `core/config/map.ts`. `app-map`: wejście `route` (linia + pas o grubości 2 × promień przeliczanej przy zoomie,
  dopasowanie widoku do trasy z pasem), style `.map-route` / `.map-corridor` w `global.scss`. Nowy `shared/components/place-search`
  (moja pozycja + Nominatim) – `LocationPicker` z niego korzysta i dostał `radiusKm` (okrąg), `markerTitle`, `hint`.
  Profil: pozycja „Alerty”. Backend bez zmian.
  Testy Vitest: `alert-form` (11), `RoutingService` + `prepareRoute` (5), `AlertsPage` (7), `AlertEditPage` (8),
  `RoutePickerComponent` (5), `PlaceSearchComponent` (3).
  Do zrobienia ręcznie: klucz ORS w `environment*.ts`, sprawdzenie trasy i pasa na telefonie.
  12.11.2026 lokalnie (`verify.cmd`, Docker): build + dotnet 468/468 (Core 198, Application 179, integracyjne 91), `npm run api`
  (klient bez zmian), lint + Vitest 250/250 + build zielone.
- [ ] Krok F10 (w toku) – wdrożenie. Część 1 (12.11.2026): lokalny test PWA i pusha przez HTTPS.
  Backend: `ICanHelp.Api/Frontend/FrontendExtensions.cs` – API serwuje zbudowany frontend z `Frontend:Path` (domyślnie
  `wwwroot`, w `.gitignore`), fallback na `index.html` (bez `/api`, `/hubs`, `/swagger`, `/health`), `no-cache` dla
  `index.html` / `ngsw.json` / workerów / manifestu, typ `.webmanifest`; bez builda w katalogu – jak dotąd (Development:
  `/` → Swagger). `FcmPushSender` loguje kody błędów FCM przy usuwaniu tokenów. `serve-local.cmd` (`scripts/serve-local.ps1`):
  `docker compose up` → `npm run build` → `www/` → `ICanHelp.Api/wwwroot` (robocopy /MIR) → API `--launch-profile https`
  (`-NoBuild` – bez builda). Testy: `FrontendHostingTests` (12 integracyjnych), przykłady w `ICanHelp.Api.http`.
  Test na żywo (Chrome przez Playwright, https://localhost:7080, certyfikat dev zaufany, push z prawdziwym FCM): service worker
  `app-sw.js` aktywny, cache ngsw, `/profile` działa offline; manifest bez błędów, Chrome zgłasza `beforeinstallprompt`
  (Lighthouse nie ma już kategorii PWA – instalowalność sprawdzona przez CDP `Page.getInstallabilityErrors`). Push: zgoda →
  token FCM → `POST api/me/devices` → oferta drugiego konta → przy otwartej aplikacji worker przekazuje push do strony (toast,
  ~0,6–2,6 s), przy zamkniętej – systemowe powiadomienie z `tag` = id i linkiem do wątku (~2 s); 4/4 na trwałym profilu Chrome.
  Uwagi: na świeżo utworzonym profilu Chrome FCM bywa, że od razu odpowiada `Unregistered` (aplikacja rejestruje token raz,
  bez usuwania – to po stronie Chrome/GCM); backend usuwa wtedy urządzenie, a aplikacja zapisze token ponownie przy następnym
  starcie. Panel przeglądarki w aplikacji Claude nie rejestruje service workerów (także na http://localhost) – testy w Chrome.
  Kliknięcia w systemowy dymek nie da się zautomatyzować – do sprawdzenia ręcznie (razem z telefonem przez tunel).
  Test autora: komputer (Chrome) OK; Brave – push wymaga włączenia „Use Google services for push messaging”; Chrome
  incognito – powiadomienia zawsze zablokowane. Android (Chrome) przez `cloudflared tunnel --url http://localhost:5080`
  (szybki tunel bez konta, `winget install Cloudflare.cloudflared`) – OK. iPhone – do zrobienia. Uwagi UX z testu → Krok F11.
  12.11.2026 lokalnie (`verify.cmd`, Docker): build + dotnet 479/479 (Core 198, Application 179, integracyjne 102),
  `npm run api` (klient bez zmian), lint + Vitest 250/250 + build zielone.
- [ ] Krok F11 (w toku) – poprawki po teście autora. Część A (12.11.2026, sam frontend): błędy i czytelność.
  Szeroki ekran: globalna klasa `page-column` (`global.scss`, `--app-column-width: 720px`) – na `ion-content` boczny
  odstęp w `::part(scroll)` (z zachowaniem `--padding-*`), na stopkach / karcie wątku `max-width` + `margin: auto`;
  dodana na wszystkich 14 stronach (warstwa mapy `slot="fixed"` zostaje na całą szerokość). Szczegóły: galeria
  `object-fit: contain` na tle, mapa `aspect-ratio: 16/9` (200 px – 50vh), metadane jako `ion-chip` (kategoria,
  „Trudność: …” w kolorze z `difficultyColor`, „N ofert”). Logo: `shared/components/brand` (`app-brand`, ikona PWA +
  „I can help U”, < 576 px sama ikona) w nagłówku każdej strony → `navigateRoot('/announcements')`. Zakładki:
  `TabsPage.openTabRoot` (`ionTabButtonClick` na `ion-tab-bar`, `stopPropagation`, `navigateRoot('/<zakładka>')`) – zawsze
  korzeń zakładki, widok korzenia używany ponownie. Lista ogłoszeń: każde kolejne `ionViewWillEnter` = pierwsza strona od
  nowa (przyczyna: strona w stosie, wczytanie tylko w `ngOnInit`). Wątek: karta ogłoszenia w nagłówku (miniatura
  z `GET api/announcements/{id}`, tytuł, status, rola, „Zobacz ogłoszenie”), w wątku z przyjętą ofertą „Rozpocznij
  zlecenie” / „Zakończ zlecenie”; `start` / `complete` (potwierdzenie + API + toast) przeniesione do `OffersService`.
  Brave: `PushPlatform.isBrave`, przy nieudanej rejestracji FCM okno z instrukcją `BRAVE_PUSH_HINT`. Testy: nowe
  `brand.component.spec`, dopisane w `tabs`, `announcements`, `announcement-details`, `conversation`, `offers.service`,
  `push.service`. Części B (nawigacja) i C (aktualizacja oferty) – osobno.
  12.11.2026 lokalnie (`verify.cmd`, Docker): dotnet 479/479 (Core 198, Application 179, integracyjne 102), `npm run api`
  (klient bez zmian), lint + Vitest 259/259 + build zielone. Podgląd `ng serve` (bez backendu): kolumna 720 px i logo
  na 1400 px, na telefonie sama ikona – OK; szczegóły z danymi i Brave – do sprawdzenia ręcznie.
  Część B (12.11.2026): nawigacja. Zakładki Szukaj (`/announcements`) · Ulubione (`/liked`) · Dodaj · Zlecenia
  (`/conversations` – adres zostaje, linki z powiadomień / pusha `/conversations/{id}` trafiają do jej stosu) · Konto
  (`/profile`). Szukaj: `ion-searchbar` → nowy filtr `phrase`, kafelki kategorii (`core/categories/category-icons.ts`,
  Ionicons po nazwie z seeda) zamiast listy rozwijanej. Backend: `GetAnnouncements.Phrase` + `GetPhrasePattern()` (trim,
  escape `%_[\`, > 100 znaków → `InvalidSearchPhraseException` 400), w handlerze `EF.Functions.Like((string)(object)…)`
  na tytule lub opisie; testy `GetAnnouncementsPhraseTests` (Application) i `PhraseSearchTests` (Api), przykład w `.http`,
  `api-client.ts` z `phrase`. Zlecenia: `features/orders` – Zlecam (moje ogłoszenia, wszystkie statusy) / Pomagam (moje
  wątki pomocnika), grupy `order-groups.ts` (W trakcie negocjacji / Przyjęte / W realizacji / Zakończone; pomocnik
  w grupie zlecenia tylko z przyjętą ofertą). Wiersz wątku – wspólny `ConversationItemComponent`; skrzynka wszystkich
  wątków → `/conversations/all`. Dzwonek `app-notifications-bell` w nagłówkach korzeni zakładek (powiadomienia bez
  zakładki, ze strzałką wstecz). „Zapytaj” w szczegółach; wątek bez ofert = „Pytania i odpowiedzi” (notka, pole
  „Zadaj pytanie…”). Konto: bez „Moich ogłoszeń” / „Polubionych”, z „Powiadomieniami”; przekierowania
  `/profile/liked` → `/liked`, `/profile/announcements` → `/conversations`; `MyAnnouncementsPage` bez trasy (do usunięcia).
  Testy: nowe `notifications-bell`, `category-icons`, `order-groups`, `orders.page`; dopisane w filtrach, store,
  `announcements.page`, `conversation.page`, `tabs`, e2e `app.spec`. Część C – osobno.
  12.11.2026 lokalnie (`verify.cmd`, Docker): dotnet 489/489 (Core 198, Application 186, integracyjne 105), `npm run api`
  (klient bez zmian względem ręcznej edycji), lint + Vitest 275/275 + build zielone. Wygląd na telefonie – do sprawdzenia ręcznie.
  Część C (13.11.2026): aktualizacja nieaktualnej oferty. Problem: po edycji ogłoszenia oferta pomocnika była
  `IsOutdated`, nikt nie mógł jej przyjąć, kontrować mogła tylko druga strona, a nowej oferty nie dało się złożyć (409 –
  jest oczekująca). Core: `Announcement.ReviseOffer` (Open, oczekująca, tylko autor, tylko nieaktualna; nowa oferta tej
  samej strony na bieżącej rewizji z `ParentOfferId`), nowy `OfferStatus.Revised` (`Offer.MarkAsRevised`), zdarzenie
  `OfferRevised`. Application: `ReviseOffer` + `ReviseOfferHandler`, `TransactionNotificationHandler` → druga strona,
  `NotificationType.OfferRevised`, tekst „Oferta zaktualizowana: X zł” w `NotificationTextFormatter`. Api:
  `POST api/announcements/{id}/offers/{offerId}/revise` → 201, przykład w `.http`. Bez migracji (enumy jako tekst).
  Front: `postReviseOffer` w `api-client.ts`, `offerActions().revise`, `offerKindLabel` (Oferta / Kontroferta /
  Zaktualizowana oferta), `OffersService.revise` (okno z notką, co się zmieniło), „Zaktualizuj ofertę” w wątku i w stopce
  szczegółów, status „Zaktualizowana”, powiadomienie `OfferRevised` (ikona `refresh-outline`). Testy: nowe
  `AnnouncementReviseOfferTests` (Core, 12), dopisane w `AnnouncementHandlersTests`, `TransactionNotificationHandlerTests`,
  `NotificationTextFormatterTests`, Api `AnnouncementEditTests` (pełny przepływ: 409 dla aktualnej i cudzej oferty, revise,
  powiadomienie, akceptacja); Vitest – `offer-labels`, `offers.service`, `conversation-timeline`, `conversation.page`,
  `announcement-details.page`, `notification-texts`.
  13.11.2026 lokalnie (`verify.cmd`, Docker): dotnet 507/507 (Core 210, Application 191, integracyjne 106), `npm run api`
  (klient zgodny z ręczną edycją), lint + Vitest 281/281 + build zielone. Przepływ w przeglądarce – do sprawdzenia ręcznie.
  Część D (13.11.2026): wątek i realizacja zlecenia. Odświeżanie: przyczyna objawu „dalej nieaktualna” – otwarty ekran
  budził tylko hub; push przy otwartej aplikacji (`showPush`) go pomijał. Nowy `NotificationsService.activity$`
  (`{announcementId, conversationId}` z huba i pusha; `PushNotice.announcementId`) → przeładowanie wątku (swój wątek albo
  ogłoszenie), szczegółów ogłoszenia (nowe) i „Zleceń”. Core: z `ReviseOffer` zniknął warunek „tylko nieaktualna”;
  `Start(actor, at)` – tylko przyjęty pomocnik; nowe `ReportDone(actor, at)` (InProgress, raz, `DoneReportedAt`);
  `Complete(actor, at)` z Accepted lub InProgress; zdarzenia `JobStarted` / `JobDoneReported` / `JobCompleted`
  (`Core/Events/JobEvents.cs`). Application: komenda `ReportAnnouncementDone` + handler, handlery cyklu z `IClock`,
  `TransactionNotificationHandler` (Job* → druga strona, link do wątku), `NotificationType.Job*`, teksty w
  `NotificationTextFormatter`. Infrastructure: kolumna `DoneReportedAt` (migracja `JobDoneReported` – czysta, bez indeksu
  przestrzennego; wygenerowana z `--configuration Release`, bo działające API blokowało `bin/Debug`), mapowanie w DTO.
  Api: `POST api/announcements/{id}/report-done`, `doneReportedAt` w `AnnouncementDto`, `announcementDoneReportedAt`
  w `ConversationDto`, przykłady w `.http`. Front: `api-client.ts` (`reportDone`, nowe pola), `features/offers/job-step.ts`
  (wspólny krok zlecenia dla wątku i szczegółów), `OffersService.reportDone` + teksty, „Zmień ofertę” / „Zaktualizuj
  ofertę”, `.job-note` w `global.scss`, ikony powiadomień `play-outline` / `flag-outline` / `star-outline`, „Zlecenia” –
  zwinięta, wyszarzona sekcja „Zakończone (N)”. Testy: nowe `AnnouncementJobTests` (Core), `job-step.spec`; dopisane /
  zmienione w testach Core (cykl, uczestnicy, `ReviseOffer`), Application (handlery, powiadomienia, teksty), Api
  (`AnnouncementFlowTests` – przepływ z powiadomieniami i 409, `Complete` z Accepted; `AnnouncementEditTests`), Vitest
  (wątek, szczegóły, zlecenia, oferty, powiadomienia).
  13.11.2026 lokalnie (`verify.cmd`, Docker): dotnet 532/532 (Core 223, Application 202, integracyjne 107), `npm run api`
  (klient zgodny z ręczną edycją), lint + Vitest 295/295 + build zielone. Przepływ w przeglądarce – do sprawdzenia ręcznie.
  Część F (13.11.2026): uwagi z testu D. Edycja ogłoszenia a oferty – bez „nieaktualna” (domena bez zmian):
  `offerStateLabel` („Czeka na odpowiedź na zmiany”), w wątku notka `respond` / `wait`, wpis „Edytowałeś ogłoszenie ·
  data, godzina · zapisano” (właściciel) / „Zleceniodawca zmienił ogłoszenie” (pomocnik) z „zobacz zmiany”,
  `threadNotice` (przycisk „… zmienił ogłoszenie – zobacz zmiany” / „… zmienił (podtrzymał) ofertę – zobacz aktualną”,
  przewinięcie i wyróżnienie), poprzednia wersja oferty zwinięta. Nowe `features/offers/offer-changes.ts`
  (`fieldChangeRows` – było → jest) i `OfferChangesModalComponent`; `OffersService.reviewChanges` / `keep`
  („Podtrzymuję ofertę” = `POST …/revise` z tymi samymi warunkami) / `showChanges`; stopka szczegółów pomocnika
  „Zobacz zmiany”. Oceny: backend `GET api/me/ratings` (`MyRatingDto` z rolą i tytułem) i `GET api/me/ratings/pending`
  (`PendingRatingDto`), `MyRatingsController`, handlery `MyRatingsQueryHandlers.cs`, bez migracji, przykłady w `.http`;
  front `features/ratings/ratings.page` (`/profile/ratings`, wpis w Koncie), ręcznie `MyRatingsClient` w `api-client.ts`.
  Szukaj: kafelek „Wszystkie”, „Bez limitu” w promieniu (`NO_RADIUS_LIMIT`), chip „Wyczyść filtry”, Lista / Mapa jako
  ikony w jednym, wyśrodkowanym i zawijanym wierszu z filtrami. `/conversations/all`: dzwonek + strzałka „Zlecenia”.
  Testy: Application `MyRatingRoleTests`, Api `MyRatingsTests` (do wystawienia → ocena → otrzymane z rolą, 401);
  Vitest – nowe `offer-changes.spec`, `ratings.page.spec`; dopisane w `conversation-timeline`, `conversation.page`,
  `offer-labels`, `offers.service`, `announcement-details.page`, `announcements.page`, `conversations.page`.
  13.11.2026 lokalnie (`verify.cmd`, Docker; działające `ICanHelp.Api.exe` zatrzymane za zgodą autora): dotnet 536/536
  (Core 223, Application 204, integracyjne 109), `npm run api` (klient zgodny z ręczną edycją), lint + Vitest 322/322
  + build zielone. Przepływ w przeglądarce – do sprawdzenia ręcznie.
  Część E (13.11.2026): rezygnacja i przerwanie zlecenia. Core: `Announcement.Abandon` (Accepted → Open, oferta →
  `OfferStatus.Abandoned`) i `Interrupt` (InProgress → `AnnouncementStatus.Interrupted`), encja `JobExit` (część agregatu,
  `JobExits`), enumy `JobExitKind` / `JobExitReason`, `InvalidJobExitException`, `HasLeftJob` (pomocnik po zerwaniu nie
  składa oferty ani wątku, jego wątek tylko do odczytu – potwierdzone z autorem), `GetInterruption`, `CreateRating`
  z Completed lub Interrupted, zdarzenia `JobAbandoned` / `JobInterrupted`, `NotificationType.JobAbandoned/JobInterrupted`.
  Application: komendy `AbandonJob` / `InterruptJob` (wspólna baza `JobExitCommand`) + handlery, powiadomienia (druga
  strona, parametry `actorRole` / `exitReason` / `note`) i teksty, DTO `JobExitDto`, `UserReliabilityDto`, query
  `GetUserReliability`, `jobExits` w szczegółach, `JobExit` w osi wątku, `interruption` w ocenach. Infrastructure: tabela
  `JobExits` (migracja `JobExits` – czysta), konwertery enumów, `GetUserReliabilityHandler`, kontakt i „do wystawienia”
  także po przerwaniu. Api: `POST api/announcements/{id}/abandon|interrupt`, `GET api/users/{id}/reliability`, przykłady
  w `.http`. Front: ręcznie `api-client.ts` (`abandon`, `interrupt`, `getReliability`, DTO), `features/offers/job-exit.ts`
  + `JobExitModalComponent`, `OffersService.exitJob`, `jobStep().exit`, wątek (wpis, notki, licznik drugiej strony),
  szczegóły (akcje, historia, licznik właściciela i pomocników, ocena po przerwaniu, „Wystaw ponownie” → `/add?copyOf=`),
  „Zlecenia” (grupy), „Oceny” (przerwane), teksty i ikony powiadomień, etykiety „Przerwane” / „Zerwana”.
  Testy: Core `AnnouncementJobExitTests`, Application (handlery, powiadomienia, teksty), Api `JobExitTests`
  (rezygnacja → Open, 400/409, wątek tylko do odczytu, powiadomienia, liczniki, przerwanie → oceny, „do wystawienia”);
  Vitest – nowe `job-exit.spec`, dopisane `job-step`, `offer-modals`, `offers.service`, `conversation.page`,
  `announcement-details.page`, `order-groups`, `notification-texts`. Pierwszy przebieg `verify.cmd`: NSwag spłaszczył
  wspólną bazę komend (`AbandonJob` / `InterruptJob` bez `JobExitCommand`) i oznaczył `interruption` jako wymagane →
  typ `JobExitCommand` przeniesiony do `job-exit.ts`, fixture ocen z `null`.
  13.11.2026 lokalnie (`verify.cmd`, Docker; działające `ICanHelp.Api` zatrzymane za zgodą autora): dotnet 574/574
  (Core 247, Application 215, integracyjne 112), `npm run api` (klient zgodny), lint + Vitest 342/342 + build zielone.
  Przepływ w przeglądarce – do sprawdzenia ręcznie.
  Część G (13.11.2026, uwaga autora po E: oceny po przerwaniu pozwalałyby nabijać opinie): `CreateRating` znów tylko
  z Completed; `JobExit.Response/RespondedAt`, `Announcement.RespondToJobExit` (druga strona, raz), zdarzenie i
  powiadomienie `JobExitResponded`, zaproszenie „Możesz dodać swoje stanowisko.” w tekstach Job*; migracja
  `JobExitResponse`. Application: komenda `RespondToJobExit` + handler, `UserProfileDto` / `RoleStatsDto` /
  `ProfileInterruptionDto`, query `GetUserProfile`; z ocen usunięte `interruption`. Infrastructure: `GetUserProfileHandler`
  (oceny i zakończone per rola, wyjścia przez niego / drugą stronę). Api: `POST …/job-exits/{number}/response`,
  `GET api/users/{id}/profile`, przykłady w `.http`. Front: `features/profile/user-profile.page` (`/users/:userId`),
  `OffersService.respondToExit`, `canRespondToExit`, „Dodaj swoje stanowisko” w wątku i szczegółach, linki do profilu,
  stopka po przerwaniu bez ocen, „Oceny” bez przerwanych, teksty powiadomień. Testy: Core (bez ocen po przerwaniu,
  stanowisko), Application (handler, powiadomienie, teksty), Api `JobExitTests` (stanowisko 400/409, profil obu stron,
  profil z ocenami per rola, 404), Vitest – nowe `user-profile.page.spec`, dopisane `job-exit`, `offers.service`,
  `conversation.page`, `announcement-details.page`, `notification-texts`.
  13.11.2026 lokalnie (`verify.cmd`, Docker): dotnet 581/581 (Core 250, Application 218, integracyjne 113), `npm run api`
  (klient wygenerowany zgodny), lint + Vitest 352/352 + build zielone. Przepływ w przeglądarce – do sprawdzenia ręcznie.
  Część H (13.11.2026, uwaga autora po G: dwa konta mogą w kółko kończyć fikcyjne zlecenia i nabijać 5 gwiazdek):
  Core – `Announcement.AcceptedAt` / `CompletedAt` (AcceptOffer / Complete, czyszczone przy Abandon; migracja
  `AnnouncementJobTimes`), nowa czysta funkcja `Services/ReputationPolicy` (`FindSuspiciousJobs`: potwierdzone < 30 min
  po przyjęciu albo kolejne z tym samym partnerem w ciągu 7 dni, obie role; `Summarize`: bez podejrzanych, potem
  najnowsza ocena od każdej osoby). Application: `RoleStatsDto` + `RaterCount`, `PartnerCount`, `MostJobsWithOnePartner`.
  Infrastructure: `GetUserProfileHandler` liczy przez politykę (jedno zapytanie o zakończone zlecenia obu ról).
  Front: `user-profile.page` – „4,8 / 5 · 12 ocen od 9 osób”, „Średnia jeszcze niepoliczona”, „14 · z 9 osobami”,
  „Zlecenia z tą samą osobą”; notka na ekranie „Oceny”. Klient API – `RoleStatsDto` (ręcznie, potem `npm run api` zgodny).
  Testy: Core `ReputationPolicyTests` (9) + czasy w `AnnouncementJobTests`, Api `JobExitTests` (profil: powtórka i szybkie
  zlecenie nie budują średniej; helper `TestApi.BackdateAcceptanceAsync` cofa `AcceptedAt` w bazie), Vitest
  `user-profile.page.spec`. Decyzja: osobne „oceny od nowych kont” – nie teraz.
  13.11.2026 lokalnie (`verify.cmd`, Docker): dotnet 593/593 (Core 261, Application 218, integracyjne 114), `npm run api`,
  lint + Vitest 354/354 + build zielone. Profil w przeglądarce – do sprawdzenia ręcznie.
- [ ] Krok 16 – „jestem w okolicy” na żądanie
- [ ] Krok 17 (później) – aplikacja w sklepach + tryb automatyczny
