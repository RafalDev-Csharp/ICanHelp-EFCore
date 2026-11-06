# ICanHelpApp – szczegóły techniczne

> Szczegóły przeniesione z `CLAUDE.md`, żeby tamten został krótki. Czytaj sekcję, której dotyczy bieżący krok.
> Sekcje: Infrastructure, Api, Uwierzytelnianie (z prywatnością), Zdjęcia, Rozmowa (z „moimi negocjacjami”).

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

## Zdjęcia (Krok 10)
- `POST api/announcements/{id}/images` (multipart, pole `file`, 201 + Location), `DELETE …/images/{imageId}` (204),
  `GET …/images/{imageId}` (publiczny plik, `Cache-Control` 1 dzień). `ImageDto.Url`, `AnnouncementDto.ThumbnailUrl` (lista: podzapytanie).
- `Application/Storage`: `IFileStorage` (Save/OpenRead/Delete po ścieżce względnej), `ImageFiles` – format z „magic bytes”
  (JPEG/PNG/WebP, nie z nazwy/Content-Type), max 5 MB (liczone przy czytaniu), ścieżka `{announcementId:N}/{imageId:N}.ext`.
- Handler: walidacja pliku → w transakcji `AddImage` (reguły) → zapis pliku; błąd po zapisie = plik kasowany. Usuwanie: plik po commicie.
- `Infrastructure/Storage/LocalFileStorage` – katalog `Storage:ImagesPath` (domyślnie `App_Data/images` w Api, w .gitignore);
  testy Api: katalog tymczasowy z `ApiFactory`. Błędy: `InvalidImage` → 400, `ImageNotFound` → 404.

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
