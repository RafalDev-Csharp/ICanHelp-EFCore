# ICanHelpApp – kontekst projektu dla asystenta AI

> Ten plik czyta agent AI na początku każdej sesji. Dzięki niemu nie musi przeszukiwać całego repo.
> Aktualizuj sekcję **Stan prac** po każdym kroku. Trzymaj plik krótki (< 150 linii) – szczegóły obszarów idą do
> `docs/ARCHITEKTURA.md`, pełny opis kroku do `docs/HISTORIA-KROKOW.md`.

## Pomysł
Marketplace drobnej pomocy sąsiedzkiej. **Zleceniodawca (Client)** publikuje ogłoszenie z problemem
(cieknący kran, malowanie ściany, powieszenie obrazu, porada). **Pomocnik (Helper)**, który zna się na rzeczy
i najlepiej często przejeżdża w pobliżu (np. wracając z pracy), składa ofertę z ceną. Strony mogą negocjować
(kontroferty). Zleceniodawca akceptuje najlepszą ofertę → zlecenie przypisane → wykonane → wzajemne oceny.

Jeden użytkownik może być raz klientem, raz pomocnikiem (rola w transakcji, nie rola konta).

## Stack
- .NET 9, C# (Nullable enabled), EF Core 9 + SQL Server
- Clean Architecture: `Core` (domena) ← `Application` (CQRS: komendy/zapytania, DTO) ← `Infrastructure` (EF, repozytoria) ← `Api` (ASP.NET Core, kontrolery)
- Testy: xUnit + Shouldly (`ICanHelp.Core.Tests`, `ICanHelp.Application.Tests` – handlery z ręcznymi fake'ami w `Fakes/`,
  `ICanHelp.Api.Tests` – integracyjne: `WebApplicationFactory` + SQL Server z Testcontainers, `[Trait("Category","Integration")]`).
  Nie FluentAssertions 8+ – ma płatną licencję komercyjną.

## Zasady w kodzie (stosuj zawsze)
1. **Core nie zależy od niczego** (żadnego EF, ASP.NET). Tylko czysty C#.
2. Encje: `private set`, prywatny bezparametrowy konstruktor dla EF, publiczny konstruktor przyjmujący **tylko wartości**
   (nigdy nawigacje ani kolekcje). Części agregatu (`Offer`, `Image`, `Subcategory`) mają konstruktor `internal` –
   tworzy je tylko korzeń (`announcement.AddOffer(...)`, `category.AddSubcategory(...)`).
3. Kolekcje: `private readonly List<T> _items = new(); public IReadOnlyCollection<T> Items => _items.AsReadOnly();`
4. Logika biznesowa w encjach (np. `announcement.AcceptOffer(offerId)`), nie w handlerach.
5. Value objects jako `sealed record`, walidacja w konstruktorze. Wyjątki są public i dziedziczą po `CustomExceptions`;
   łamanie reguły biznesowej → `DomainRuleException`. Identyfikatory to VO `Id` i `UserId` (Guid, nie może być pusty).
6. Daty: `DateTime` UTC. Domena **nie woła** `DateTime.UtcNow` – czas dostaje parametrem (`createdAtUtc`), co ułatwia testy.
   W Application czas daje `IClock` (`Application/Time`, implementacja `Infrastructure/Time/Clock`). Nie waliduj „przeszłości” w VO – waliduj w metodzie domenowej.
7. Status zamiast wielu booli (`AnnouncementStatus`, `OfferStatus`).
8. Konfiguracja EF: każdą relację konfiguruj **raz** (relacje wewnątrz agregatu – w konfiguracji korzenia agregatu).
   Konwertery VO globalnie w `ConfigureConventions`. VO z kilkoma polami jako `ComplexProperty`/`OwnsOne`.
9. Na SQL Server unikaj wielu ścieżek kaskadowych – domyślnie `DeleteBehavior.Restrict`, `Cascade` tylko dla części agregatu.
10. Async wszędzie (`Task`, `CancellationToken`), repozytorium per agregat (interfejs w Core, implementacja w Infrastructure).
    Repozytoria **nie** wołają `SaveChanges` – robi to `IUnitOfWork.ExecuteAsync(...)` (interfejs w `Core/Repositories`,
    implementacja `SqlUnitOfWork` = transakcja + SaveChanges).
11. Nazewnictwo po angielsku w kodzie, komentarze mogą być po polsku.

## Model domeny (zaimplementowany w Core)
- `User` (agregat): UserName, FullName, PasswordHash, Role (enum User/Admin), CreatedAt, IsLocked, Description?,
  ContactData (VO: Email, PhoneNumber, Country, City, PostalCode – w tabeli Users, Email unikalny)
- `Announcement` (agregat): OwnerId, CategoryId, SubcategoryId, Difficulty (enum), Title, Description, Budget (Price),
  Location (VO lat/lon), Status, CreatedAt, AcceptedOfferId?, **Offers**, **Images** (max 5),
  **Revision** (0 = opublikowane, +1 przy realnej edycji; token współbieżności), UpdatedAt?, **Changes** (`AnnouncementChange`: rewizja, czas, `AnnouncementFields` [Flags])
  - metody: `Update(actor, ..., at)` (tylko właściciel, `Open`), `GetChangesSince(rev)`, `AddOffer`, `CounterOffer`, `AcceptOffer`, `RejectOffer`, `WithdrawOffer`, `AddImage(actor,…,at)`, `RemoveImage(actor, id, at)`, `GetThumbnail()`,
    `Start(actor)`, `Complete(actor)`, `Cancel(actor)`, `GetAcceptedHelperId()`, `IsParticipant(user)`, `CreateRating(...)`,
    `GetOffersVisibleTo(viewer?)`, `StartConversation(...)`, `AllowsConversationWith(helper)`
  - `Offer` (część agregatu): HelperId, ProposedBy (Client/Helper), Price, ProposedStart/End, Message?, Status, ParentOfferId?
    (kontroferta = nowy `Offer` wskazujący poprzedni; poprzedni dostaje status `Countered`)
  - `Image` (część agregatu): FileName (do wyświetlania), FileExtension, FilePath (ścieżka w magazynie, nie trafia do DTO), AddedAt
    (najstarsze = miniatura). Dodanie/usunięcie = nowa rewizja z flagą `Images` (stare/nowe = lista id zdjęć „id1, id2”).
- `Category` (agregat) → `Subcategory` (część agregatu, unikalna nazwa w kategorii)
- `RefreshToken` (agregat, `Entities/Users`): UserId, TokenHash (SHA-256), ExpiresAt, RevokedAt?, ReplacedByTokenId? – `Rotate`, `Revoke`.
- `Rating`: AnnouncementId, RaterId, RateeId, Rate (1–5), Comment?; unikalne (AnnouncementId, RaterId)
- `LikedAnnouncement` (UserId, AnnouncementId) i `LikedUser` (LikerId, LikedId) – klucze złożone, istnienie wiersza = polubione
- `Conversation` (agregat, `Entities/Negotiations`): AnnouncementId, ClientId (= właściciel), HelperId, CreatedAt, `Client/HelperLastReadAt`,
  **Messages** (`ConversationMessage`: SenderId, Text (Description ≤ 500), SentAt, AnnouncementRevision). Jeden na parę (AnnouncementId, HelperId).
  Tworzy go `announcement.StartConversation` (tylko Open, nie właściciel); `SendMessage(..., announcement, at)` sprawdza
  `announcement.AllowsConversationWith(HelperId)`: Open – każdy, Accepted/InProgress – tylko zaakceptowany pomocnik, Completed/Cancelled – nikt
  (wątek tylko do odczytu; po Completed zostają oceny).
  `MarkAsRead` (znacznik nie cofa się; wysłanie = przeczytane), `CountUnread`. Rozmowa ≠ oferta (wiadomości nie da się zaakceptować).
- `AggregateRoot` (baza `Announcement`, `Conversation`): `DomainEvents` – zdarzenia (`Core/Events`: `AnnouncementPublished/Edited`,
  `OfferMade/Countered/Accepted/Rejected/Withdrawn`, `MessageSent`), czas = parametr metody. `SqlUnitOfWork` przenosi je do outboksa.
- `Notification` (agregat, `Entities/Notifications`): UserId (odbiorca), Type, Payload (JSON parametrów), AnnouncementId?, ConversationId?,
  CreatedAt, ReadAt?, SourceEventId (unikalne z UserId). Powstaje w tle z outboksa → `docs/ARCHITEKTURA.md` „Powiadomienia”.
- `AnnouncementAlert` (agregat, `Entities/Alerts`): Area (1 punkt) | Route (2–1000), kategoria + podkategorie, RadiusKm 0,5–50, dni/godziny,
  IsEnabled, max 10/użytkownik. Geometria tylko w bazie (`Shape` z EF, `Zone` z triggera SQL) → `docs/ARCHITEKTURA.md` „Alerty”.

## Przepływ statusów
Announcement: `Open` → (AcceptOffer) `Accepted` → (Start) `InProgress` → (Complete) `Completed`; `Open`/`Accepted` → `Cancelled`
Offer: `Pending` → `Accepted` | `Rejected` | `Withdrawn` | `Countered` (zastąpiona kontrofertą)
Akceptuje/odrzuca/kontruje zawsze **druga strona** niż autor oferty; wycofać może tylko autor.
Oferty przyjmowane tylko w statusie `Open`. Po akceptacji pozostałe `Pending` → `Rejected`.
Oferta pamięta `AnnouncementRevision`; złożona przed ostatnią edycją jest nieaktualna (`OfferDto.IsOutdated`) i **nie da się jej
zaakceptować** – trzeba kontroferty. Plan dalszych kroków i uzasadnienie: `docs/PLAN-NEGOCJACJE.md`.
`Start` – właściciel albo zaakceptowany pomocnik; `Complete` i `Cancel` – tylko właściciel. Ocena (`CreateRating`) tylko po `Completed`,
przez uczestnika; ocenianego (druga strona) wybiera domena. „Raz na ogłoszenie” – handler (`AlreadyRatedException`) + unikalny indeks.

## Gdzie co leży
`docs/`: `ARCHITEKTURA.md` (szczegóły obszarów), `PLAN-NEGOCJACJE.md` (Kroki 9–12), `PLAN-POWIADOMIENIA.md` (Kroki 13–17, decyzja o kliencie: PWA), `PLAN-FRONTEND.md` (Kroki F1–F10), `HISTORIA-KROKOW.md` (pełny stan prac).
```
Core            Entities, ValueObjects, Enums, Events, Exceptions, Repositories (interfejsy + IUnitOfWork)
Application     Abstractions (ICommand/IQuery + handlery), DTO, Queries, Commands (+ Commands/Handlers), Exceptions,
                Events (IDomainEventHandler), Notifications (handlery zdarzeń, NotificationPublisher, INotificationSender, IEmailSender,
                NewAnnouncementAlertHandler), Alerts (AlertOptions: limit dzienny, cisza nocna, strefa czasowa),
                Security (IPasswordManager, IAuthenticator, ITokenStorage), Time (IClock), Extensions.AddApplication()
Infrastructure  DAL/ICanHelpDbContext, DAL/Converters, DAL/Configurations, DAL/Repositories,
                DAL/Handlers (query handlery + mapowanie AsDto), SqlUnitOfWork, DbInitializer, Security (JWT, hasła), Time,
                Outbox (OutboxMessage, OutboxProcessor, OutboxBackgroundService), Notifications (LoggingEmailSender),
                Extensions.AddInfrastructure()
Api             Program.cs, Controllers (Alerts, Announcements, Categories, Conversations, Notifications, Users), ErrorHandling/DomainExceptionHandler,
                Notifications (NotificationsHub /hubs/notifications, SignalRNotificationSender),
                appsettings*.json, Properties/launchSettings.json, ICanHelp.Api.http (przykładowe żądania)
Web             frontend PWA (Angular + Ionic) – własny `ICanHelp.Web/CLAUDE.md` (stack, struktura, komendy npm)
Api.Tests       Infrastructure/ApiFactory (kontener + ApiCollection), Infrastructure/TestApi (CreateUserAsync, ShouldBeProblemAsync…),
                testy per obszar: ApiBasics, Users, Session, AnnouncementFlow, AnnouncementEdit, AnnouncementImage, Conversation, DistanceSearch,
                Notification (outbox wyłączony, `ProcessOutboxAsync()`), Alert
.github/workflows/ci.yml   job backendu: build + testy jednostkowe + integracyjne (ubuntu-latest, Docker); job `frontend`: npm ci, lint, test, build
```
Testy integracyjne: jeden kontener i baza `ICanHelpTests` na cały przebieg, środowisko `Testing`, config przez `UseSetting`.
Bazy nie czyścimy – każdy test tworzy własnych użytkowników (unikalny e-mail) i filtruje po swoich danych (`ownerId`).
Zapytania (`GetX`) definiuje Application, a ich handlery są w Infrastructure, bo czytają prosto z DbContext.
Handlery komend są w Application (`Commands/Handlers`), używają repozytoriów + `IUnitOfWork`. Rejestracja handlerów: Scrutor.
Wspólne: `RepositoryExtensions` (`GetOrThrowAsync` → `AnnouncementNotFound`, `GetActiveOrThrowAsync` → `UserNotFound`/`UserLocked`),
`Time/DateTimeExtensions.AsUtc()` (daty z JSON bez strefy = UTC).

## Infrastructure i Api w skrócie (szczegóły: `docs/ARCHITEKTURA.md`)
- EF: konwertery VO i enumy jako string w `ConfigureConventions`; `Price`/`Location` → `ComplexProperty`, `ContactData` → `OwnsOne`.
  Relacje między agregatami tylko przez FK, bez nawigacji. Repozytoria ładują cały agregat; odczyt – handlery w `DAL/Handlers`
  (`AsNoTracking`, mapowanie w `Handlers/Extensions.cs`). `DbInitializer`: migracje + seed kategorii przy starcie.
- Migracje w `Infrastructure/DAL/Migrations`, od Kroku 7 pisane ręcznie. EF nie zna indeksu przestrzennego `IX_Announcements_GeoPoint`
  (kolumna `GeoPoint`) – przy generowaniu migracji usuń linie, które go dotykają.
- Kontrolery: handler wstrzykiwany do akcji `[FromServices] IQueryHandler<,>` / `ICommandHandler<>` (bez mediatora),
  `null` z query → 404. Id aktora zawsze z tokenu (`User.TryGetUserId`), pola komend ustawiane przez kontroler mają `[JsonIgnore]`.
- Błędy jako ProblemDetails (`DomainExceptionHandler`): *NotFound → 404, `DomainRuleException`/`ConcurrencyConflict`/`AlreadyRated`/`*AlreadyInUse` → 409,
  inne `CustomExceptions` → 400, `InvalidCredentials` → 401, `UserLocked` → 403; `errorCode` = nazwa wyjątku bez „Exception”, zawsze `traceId`.
- Auth: JWT HS256 (`Auth:SigningKey` ≥ 32 bajty, poza Development wymagany z env). Prywatność: oferty widzi właściciel (wszystkie)
  i pomocnik (swoje), reszta tylko `HelperCount`; kontakt po akceptacji; wątki tylko dla dwóch uczestników (obcy → 404).
- Refresh tokeny (F2): `RefreshToken` (tylko hash), rotacja przy każdym `refresh`, wykrycie ponownego użycia → `docs/ARCHITEKTURA.md`.
- Endpointy: `api/users` (+ `refresh`, `sign-out`), `api/categories`, `api/announcements` (+ offers, images, conversations, contact, ratings, like),
  `api/conversations/{id}`, `api/me/conversations`, `api/me/notifications` (+ hub `/hubs/notifications`), `api/me/alerts`. Przykłady wszystkich w `ICanHelp.Api.http`. Porty 5080/7080, `/health`, Swagger w Development.

## Komendy
```bash
verify.cmd                                    # wszystko po kroku (Windows) → .verify/report.md; -Quick = bez Dockera
dotnet build
dotnet test                                   # integracyjne wymagają działającego Dockera
dotnet test --filter "Category!=Integration"  # tylko jednostkowe
docker compose up -d                # SQL Server w Dockerze
dotnet tool restore                 # instaluje dotnet-ef z manifestu
dotnet run --project ICanHelp.Api   # Swagger: http://localhost:5080/swagger; przy starcie DbInitializer robi migracje + seed
dotnet ef migrations add <Name> -p ICanHelp.Infrastructure -s ICanHelp.Api -o DAL/Migrations
dotnet ef database update      -p ICanHelp.Infrastructure -s ICanHelp.Api
cd ICanHelp.Web && npm ci && npm start   # frontend http://localhost:4200 (proxy /api, /hubs → :5080); npm run lint / npm test / npm run build
```

## Stan prac (aktualizuj!) – pełna historia: `docs/HISTORIA-KROKOW.md`
- [x] Kroki 1–8: Infrastructure pod nowy Core, testy Core, Api + Swagger + ProblemDetails, Docker + migracja `Init`,
  JWT (sign-up/sign-in), komendy ogłoszeń/ofert/ocen, wyszukiwanie po odległości, testy integracyjne + CI. Zweryfikowane (172/172).
  **Zostało: push + zielony build w GitHub Actions.**
- [x] Krok 9 – PATCH ogłoszenia + rewizje + blokada akceptacji nieaktualnej oferty (195/195).
- [x] Krok 10 – zdjęcia (241/241).
- [x] Krok 11 – wątek negocjacji + prywatność ofert, migracja `Conversations`.
- [x] Krok 12 – „moje negocjacje”: `GET api/me/conversations`, bez migracji. 6.11.2026: build + 286/286 testów zielone
  (potwierdza też Krok 11).
- [x] Krok 13 – powiadomienia: zdarzenia domenowe, outbox, `Notifications`, SignalR, `api/me/notifications`, migracja `Notifications`.
  8.11.2026: build + 320/320 testów zielone.
- [x] Krok 14 – alerty o nowych ogłoszeniach: obszar i trasa, `api/me/alerts`, migracja `AnnouncementAlerts` (trigger + indeks przestrzenny).
  8.11.2026: build + 371/371 testów zielone.
- [ ] Krok 15 – kanał push (FCM) + `api/me/devices` – `docs/PLAN-POWIADOMIENIA.md`
- [ ] Krok 16 – „jestem w okolicy” na żądanie (alert `Presence` z wygaśnięciem, trasa „aktywuj teraz”), bez śledzenia w tle
- [ ] Krok 17 (później) – Capacitor (sklepy) + opcjonalna lokalizacja w tle
- [ ] Frontend (PWA, Angular + Ionic): Kroki F1–F10 i kolejność z backendem – `docs/PLAN-FRONTEND.md`
  - [x] F1 – szkielet `ICanHelp.Web` (Angular 22 + Ionic 9, 5 zakładek, core/shared/features, pipe'y pl-PL, proxy, job CI `frontend`).
    8.11.2026: lint + test + build zielone, aplikacja uruchomiona.
  - [x] F2 – klient NSwag (`npm run api`), `AuthService` + interceptory (Bearer/odświeżanie, ProblemDetails → toast) + `authGuard`,
    ekrany logowania/rejestracji/profilu; backend: refresh tokeny (`api/users/refresh`, `sign-out`, migracja `RefreshTokens`).
    8.11.2026: dotnet 400/400, lint + 65/65 Vitest + build zielone.
  - [x] F3 – przeglądanie: lista (filtry, „blisko mnie”, przewijanie bez końca) ↔ mapa (Leaflet), szczegóły (galeria, mapa,
    oceny właściciela, „Złóż ofertę”/„Napisz” → logowanie). Zadania backendowe: `helperCount` na liście, polubienia (`PLAN-FRONTEND.md`).
    9.11.2026: `npm install` (leaflet w package-lock) + lint + test + build zielone.
  - [x] F4 – dodawanie/edycja ogłoszenia (formularz, GPS / mapa / Nominatim, zdjęcia zmniejszane do 1600 px), „Moje ogłoszenia”,
    „Polubione”, anulowanie; backend: `helperCount` na liście, `PUT/DELETE …/like` + `isLiked`, `expectedRevision` w PATCH (409).
    9.11.2026: build + dotnet 410/410, `npm run api`, lint + 116/116 Vitest + build zielone.

## Jak pracować z AI (oszczędnie)
- Jedna sesja = jeden krok z listy. Na start: „Przeczytaj CLAUDE.md i docs/PLAN-…md, zrób Krok N” (frontend: Krok FN).
- Podawaj konkretne pliki, zamiast prosić o przeszukanie repo.
- Po kroku: jedna linia w „Stan prac”, szczegóły w `docs/HISTORIA-KROKOW.md` (i `docs/ARCHITEKTURA.md`, jeśli doszedł obszar).
- Sesja lokalna (Claude Code desktop, środowisko „Local”, Windows): AI samo uruchamia `verify.cmd` po kroku, czyta
  `.verify/report.md` i poprawia błędy aż do „WSZYSTKO OK”; uprawnienia do poleceń – `.claude/settings.json` (commit/push pyta).
- Weryfikacja w sesji w chmurze (bez .NET/Dockera/npm): autor uruchamia `verify.cmd` (build, testy jednostkowe i integracyjne,
  `npm run api`, lint/test/build frontendu) i pisze „sprawdź raport”. AI czyta `.verify/report.md` (logi: `.verify/*.log`),
  poprawia błędy, a gdy wszystko OK – wpisuje wynik (liczby testów, data) do „Stan prac” i historii. Commit robi autor.
