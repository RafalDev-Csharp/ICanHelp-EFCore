# ICanHelpApp – kontekst projektu dla asystenta AI

> Ten plik czyta agent AI na początku każdej sesji. Dzięki niemu nie musi przeszukiwać całego repo.
> Aktualizuj sekcję **Stan prac** po każdym kroku. Trzymaj plik krótki (< 150 linii).

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
- `Rating`: AnnouncementId, RaterId, RateeId, Rate (1–5), Comment?; unikalne (AnnouncementId, RaterId)
- `LikedAnnouncement` (UserId, AnnouncementId) i `LikedUser` (LikerId, LikedId) – klucze złożone, istnienie wiersza = polubione
- `Conversation` (agregat, `Entities/Negotiations`): AnnouncementId, ClientId (= właściciel), HelperId, CreatedAt, `Client/HelperLastReadAt`,
  **Messages** (`ConversationMessage`: SenderId, Text (Description ≤ 500), SentAt, AnnouncementRevision). Jeden na parę (AnnouncementId, HelperId).
  Tworzy go `announcement.StartConversation` (tylko Open, nie właściciel); `SendMessage(..., announcement, at)` sprawdza
  `announcement.AllowsConversationWith(HelperId)`: Open – każdy, Accepted/InProgress – tylko zaakceptowany pomocnik, Completed/Cancelled – nikt
  (wątek tylko do odczytu; po Completed zostają oceny).
  `MarkAsRead` (znacznik nie cofa się; wysłanie = przeczytane), `CountUnread`. Rozmowa ≠ oferta (wiadomości nie da się zaakceptować).
- Później: `HelperProfile` z obszarem działania / trasą dojazdu (punkt + promień) → wyszukiwanie „po drodze”

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
```
Core            Entities, ValueObjects, Enums, Exceptions, Repositories (interfejsy + IUnitOfWork)
Application     Abstractions (ICommand/IQuery + handlery), DTO, Queries, Commands (+ Commands/Handlers), Exceptions,
                Security (IPasswordManager, IAuthenticator, ITokenStorage), Time (IClock), Extensions.AddApplication()
Infrastructure  DAL/ICanHelpDbContext, DAL/Converters, DAL/Configurations, DAL/Repositories,
                DAL/Handlers (query handlery + mapowanie AsDto), SqlUnitOfWork, DbInitializer, Security (JWT, hasła), Time,
                Extensions.AddInfrastructure()
Api             Program.cs, Controllers (Announcements, Categories, Conversations, Users), ErrorHandling/DomainExceptionHandler,
                appsettings*.json, Properties/launchSettings.json, ICanHelp.Api.http (przykładowe żądania)
Api.Tests       Infrastructure/ApiFactory (kontener + ApiCollection), Infrastructure/TestApi (CreateUserAsync, ShouldBeProblemAsync…),
                testy per obszar: ApiBasics, Users, AnnouncementFlow, AnnouncementEdit, AnnouncementImage, Conversation, DistanceSearch
.github/workflows/ci.yml   build + testy jednostkowe + integracyjne (ubuntu-latest, Docker)
```
Testy integracyjne: jeden kontener i baza `ICanHelpTests` na cały przebieg, środowisko `Testing`, config przez `UseSetting`.
Bazy nie czyścimy – każdy test tworzy własnych użytkowników (unikalny e-mail) i filtruje po swoich danych (`ownerId`).
Zapytania (`GetX`) definiuje Application, a ich handlery są w Infrastructure, bo czytają prosto z DbContext.
Handlery komend są w Application (`Commands/Handlers`), używają repozytoriów + `IUnitOfWork`. Rejestracja handlerów: Scrutor.
Wspólne: `RepositoryExtensions` (`GetOrThrowAsync` → `AnnouncementNotFound`, `GetActiveOrThrowAsync` → `UserNotFound`/`UserLocked`),
`Time/DateTimeExtensions.AsUtc()` (daty z JSON bez strefy = UTC).

## Infrastructure (jak jest zbudowane)
- `ICanHelpDbContext.ConfigureConventions` – globalne konwertery VO (`DAL/Converters`) i enumy jako string.
- `Price`, `Location` → `ComplexProperty`; `ContactData` → `OwnsOne` (indeks na Email).
- Relacje między agregatami tylko przez FK (`HasOne<User>().WithMany()`), bez nawigacji. Cascade tylko wewnątrz agregatu.
- Repozytoria per agregat (`DAL/Repositories`) – ładują cały agregat (`Include` Offers/Images).
- Strona odczytu: handlery zapytań w `DAL/Handlers` czytają z DbContext (`AsNoTracking`) i mapują do DTO (`Handlers/Extensions.cs`).
- `DbInitializer` (IHostedService) – przy starcie: migracje + seed kategorii (nic nie robi, dopóki nie ma żadnej migracji).
- Connection string: `ConnectionStrings:DefaultConnection` (w appsettings projektu Api).
- Migracje w `ICanHelp.Infrastructure/DAL/Migrations`. `dotnet-ef` jako lokalne narzędzie (`.config/dotnet-tools.json`, 9.0.11).
- Odległość (Krok 7): Core zostaje przy `Location` (lat/lon). W bazie kolumna `GeoPoint` (`geography`, computed PERSISTED
  `geography::Point([Latitude],[Longitude],4326)`) jako shadow property + indeks przestrzenny `IX_Announcements_GeoPoint`
  (ręczny SQL w migracji `AnnouncementGeoPoint` – EF go nie modeluje, nie ma go w snapshocie). `UseSqlServer(..., x => x.UseNetTopologySuite())`,
  pomocnik `DAL/Spatial.cs` (SRID, `CreatePoint(lat, lon)` – w NTS X = długość!). `GetAnnouncements`: `Latitude`+`Longitude`
  (+ `RadiusKm`, domyślnie 10, max 100) → `IsWithinDistance`, sortowanie od najbliższych, `AnnouncementDto.DistanceKm`.
  Walidacja w `GetAnnouncements.GetDistanceFilter()` → `InvalidDistanceFilterException` (400).

## Zdjęcia (Krok 10)
- `POST api/announcements/{id}/images` (multipart, pole `file`, 201 + Location), `DELETE …/images/{imageId}` (204),
  `GET …/images/{imageId}` (publiczny plik, `Cache-Control` 1 dzień). `ImageDto.Url`, `AnnouncementDto.ThumbnailUrl` (lista: podzapytanie).
- `Application/Storage`: `IFileStorage` (Save/OpenRead/Delete po ścieżce względnej), `ImageFiles` – format z „magic bytes”
  (JPEG/PNG/WebP, nie z nazwy/Content-Type), max 5 MB (liczone przy czytaniu), ścieżka `{announcementId:N}/{imageId:N}.ext`.
- Handler: walidacja pliku → w transakcji `AddImage` (reguły) → zapis pliku; błąd po zapisie = plik kasowany. Usuwanie: plik po commicie.
- `Infrastructure/Storage/LocalFileStorage` – katalog `Storage:ImagesPath` (domyślnie `App_Data/images` w Api, w .gitignore);
  testy Api: katalog tymczasowy z `ApiFactory`. Błędy: `InvalidImage` → 400, `ImageNotFound` → 404.

## Uwierzytelnianie (Krok 5)
- `POST api/users/sign-up` (komenda `SignUp`, 201 + Location; rola zawsze `User`), `POST api/users/sign-in` (`SignIn` e-mail+hasło
  → `JwtDto`), `GET api/users/me` (`[Authorize]`). Komendy nic nie zwracają: `SignInHandler` odkłada token do `ITokenStorage` (scoped),
  kontroler go odczytuje.
- Hasła: `PasswordManager` = `PasswordHasher<User>` z ASP.NET Core Identity (PBKDF2). Plain hasło tylko w VO `Password` (walidacja).
- JWT: `Authenticator` (`JsonWebTokenHandler`, HS256), claimy `sub`/`unique_name`/`role`/`jti` – nazwy w `Security/AuthClaims`,
  `MapInboundClaims = false`. Konfiguracja `Auth` (Issuer, Audience, Expiry, SigningKey ≥ 32 bajty, `ValidateOnStart`).
  Dev-klucz w `appsettings.Development.json`; poza Development **trzeba** ustawić `Auth__SigningKey`, inaczej API nie wstanie.
- Błędy: `UserNameAlreadyInUse`/`EmailAlreadyInUse` → 409, `InvalidCredentials` → 401 (bez zdradzania, czy konto istnieje),
  `UserLocked` → 403 (dopiero po poprawnym haśle). Infrastructure ma `FrameworkReference Microsoft.AspNetCore.App` + pakiet JwtBearer.
- Swagger: przycisk Authorize (Bearer). Przykłady w `ICanHelp.Api.http`.
- Prywatność: `GET api/users/{id}` zwraca `UserPublicDto` (bez kontaktu, roli, blokady; query `GetUserPublicById`).
  Pełny `UserDto` tylko w `/me` i w `GET api/users` (`[Authorize(Roles = "Admin")]`). `liked-*` – tylko właściciel lub admin
  (`IsSelfOrAdmin` w kontrolerze, inaczej 403). Oceny, ogłoszenia, kategorie – publiczne.
  Kontakt drugiej strony: `GET api/announcements/{id}/contact` (query `GetAnnouncementContact`) – tylko uczestnik, status
  Accepted/InProgress/Completed, inaczej 404. Id aktora w komendach zawsze z tokenu (`User.TryGetUserId`, `Api/Security`), pola `[JsonIgnore]`.
  Oferty w `GET api/announcements/{id}` (token opcjonalny → `GetAnnouncementById.ViewerId`): właściciel – wszystkie, pomocnik – swoje,
  reszta (też anonim) – pusta lista + `HelperCount` (ilu pomocników złożyło ofertę). Reguła w `Announcement.GetOffersVisibleTo`.

## Rozmowa (Krok 11)
- `POST api/announcements/{id}/conversations` – pomocnik zakłada wątek (201 + Location `api/conversations/{id}`; istniał → 200 z jego id;
  kontroler po komendzie czyta id query `GetConversationId`). `MakeOffer` zakłada wątek automatycznie (`GetOrStartAsync` w `RepositoryExtensions`).
- `api/conversations` (`[Authorize]`): `GET {id}` (`ConversationDto`: `Timeline` = Message/Offer/AnnouncementChanged od najstarszych – oferty
  tego pomocnika, zmiany ogłoszenia po `CreatedAt` wątku; `MyRole`, `UnreadCount`, `CanWrite`; obcy → 404), `POST {id}/messages` (201),
  `POST {id}/read` (204). Obcy przy zapisie → 409 (DomainRule), brak wątku → 404 `ConversationNotFound`.
- `GET api/me/conversations` (Krok 12, query `GetMyConversations`, handler `GetMyConversationsHandler`): `MyConversationDto` –
  ogłoszenie (tytuł, miniatura, status), `myRole`, `otherParty`, `lastOffer` (+ `isOutdated`), `lastMessagePreview`, `unreadCount`,
  `announcementChangedSinceLastRead` (tylko pomocnik), sortowanie po `lastActivityAt`; filtry `role`, `status`, `page`, `pageSize`.
  Jedno zapytanie SQL z podzapytaniami, sort/stronicowanie w pamięci (patrz komentarz w handlerze).
- Tabele `Conversations` (unikalny indeks AnnouncementId+HelperId, indeksy ClientId/HelperId) i `ConversationMessages` (indeks ConversationId+SentAt),
  Cascade z ogłoszenia. Migracja `Conversations` zakłada wątki dla istniejących ofert.

## Api (jak jest zbudowane)
- Kontrolery `[ApiController]`, trasy `api/<zasób>`. Handler wstrzykiwany do akcji przez `[FromServices] IQueryHandler<TQuery, TResult>`
  (bez mediatora). Zapytanie z filtrami bindowane `[FromQuery]` prosto do klasy z Application (`GetAnnouncements`, `GetUsers`).
  `null` z handlera → `NotFound()`. Komendy: `[FromBody]` + `[FromServices] ICommandHandler<T>`. Dostęp do GET-ów – sekcja „Uwierzytelnianie”.
- Komendy ogłoszeń (wszystkie `[Authorize]`, `api/announcements`): `POST` (201 + `CreatedDto`), `POST {id}/offers` (201),
  `POST {id}/offers/{offerId}/counter|accept|reject|withdraw`, `POST {id}/start|complete|cancel` (204), `POST {id}/ratings` (201).
  Akcje bez body idą przez prywatne `Execute(userId => komenda, ...)` w kontrolerze. Przykłady całego przepływu w `ICanHelp.Api.http`.
- Błędy zawsze jako ProblemDetails: `AddProblemDetails` + `UseExceptionHandler` + `UseStatusCodePages`.
  `DomainExceptionHandler`: `OfferNotFound`/`AnnouncementNotFound`/`UserNotFound`/`ImageNotFound`/`ConversationNotFound` → 404, `DomainRuleException`, `AlreadyRated` → 409,
  wyjątki auth (wyżej), inne `CustomExceptions` (np. `InvalidCategory`) → 400
  (pole `errorCode` = nazwa wyjątku bez „Exception”), reszta → 500 bez szczegółów. Każda odpowiedź ma `traceId`.
- Enumy w JSON jako string (`JsonStringEnumConverter`). Swagger (Swashbuckle) tylko w Development, z komentarzy `///`.
- `GET /health` – health check (bez sprawdzania bazy). Porty: http 5080, https 7080. W Development `/` → `/swagger`,
  `UseHttpsRedirection` tylko poza Development.
- Connection string (`appsettings.json`) wskazuje SQL Server z `docker-compose.yml` (localhost,1433, sa / hasło dev).
  Na produkcji nadpisać zmienną `ConnectionStrings__DefaultConnection`.
- `public partial class Program` – pod `WebApplicationFactory` w testach integracyjnych.

## Komendy
```bash
dotnet build
dotnet test                                   # integracyjne wymagają działającego Dockera
dotnet test --filter "Category!=Integration"  # tylko jednostkowe
docker compose up -d                # SQL Server w Dockerze
dotnet tool restore                 # instaluje dotnet-ef z manifestu
dotnet run --project ICanHelp.Api   # Swagger: http://localhost:5080/swagger; przy starcie DbInitializer robi migracje + seed
dotnet ef migrations add <Name> -p ICanHelp.Infrastructure -s ICanHelp.Api -o DAL/Migrations
dotnet ef database update      -p ICanHelp.Infrastructure -s ICanHelp.Api
```

## Stan prac (aktualizuj!)
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
  `Api.Tests/ConversationTests.cs`. Pisane bez kompilatora (sesja w chmurze) – **do zrobienia u autora: `dotnet build` + `dotnet test`, commit.**
- [x] Krok 12 – „moje negocjacje”: `GET api/me/conversations` (sekcja „Rozmowa”), bez migracji. Testy: `Api.Tests/MyConversationsTests.cs`,
  `Application.Tests/Queries/MyConversationPreviewTests.cs`. Pisane bez kompilatora – **do zrobienia u autora: `dotnet build` + `dotnet test`
  (razem z Krokiem 11), commit.**
- [ ] Krok 13 – powiadomienia (opcjonalnie): `docs/PLAN-NEGOCJACJE.md`

## Jak pracować z AI (oszczędnie)
- Jedna sesja = jeden krok z listy. Na start: „Przeczytaj CLAUDE.md, zrób Krok N”.
- Podawaj konkretne pliki, zamiast prosić o przeszukanie repo.
- Po kroku: zaktualizuj „Stan prac”, commit.
