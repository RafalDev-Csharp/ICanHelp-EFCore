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
  Zweryfikowane u autora 5.11.2026: `dotnet test` – 172/172 zielone (potwierdza też Kroki 5–7). **Zostało: push + zielony build w Actions.**

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
- [ ] Krok 15 – kanał push (FCM): `docs/PLAN-POWIADOMIENIA.md`
- [ ] Krok 16 – „jestem w okolicy” na żądanie
- [ ] Krok 17 (później) – aplikacja w sklepach + tryb automatyczny
