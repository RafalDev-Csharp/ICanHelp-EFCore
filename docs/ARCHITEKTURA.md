# ICanHelpApp – szczegóły techniczne

> Szczegóły przeniesione z `CLAUDE.md`, żeby tamten został krótki. Czytaj sekcję, której dotyczy bieżący krok.
> Sekcje: Model domeny, Infrastructure, Api, Uwierzytelnianie (z prywatnością), Zdjęcia, Rozmowa, Powiadomienia,
> Push i urządzenia, Alerty.

## Model domeny (Core)
- `User` (agregat): UserName, FullName, PasswordHash, Role (enum User/Admin), CreatedAt, IsLocked, Description?,
  ContactData (VO: Email, PhoneNumber, Country, City, PostalCode – w tabeli Users, Email unikalny).
- `Announcement` (agregat): OwnerId, CategoryId, SubcategoryId, Difficulty (enum), Title, Description, Budget (Price),
  Location (VO lat/lon), Status, CreatedAt, AcceptedOfferId?, **Offers**, **Images** (max 5),
  **Revision** (0 = opublikowane, +1 przy realnej edycji; token współbieżności), UpdatedAt?, **Changes** (`AnnouncementChange`:
  rewizja, czas, `AnnouncementFields` [Flags]), DoneReportedAt? (F11 D: pomocnik zgłosił wykonanie).
  - metody: `Update(actor, ..., at)` (tylko właściciel, `Open`), `GetChangesSince(rev)`, `AddOffer`, `CounterOffer`, `ReviseOffer`, `AcceptOffer`,
    `RejectOffer`, `WithdrawOffer`, `AddImage(actor,…,at)`, `RemoveImage(actor, id, at)`, `GetThumbnail()`, `Start(actor, at)`
    (tylko przyjęty pomocnik → `JobStarted`), `ReportDone(actor, at)` (przyjęty pomocnik, InProgress, raz → `JobDoneReported`),
    `Complete(actor, at)` (właściciel, z Accepted lub InProgress → `JobCompleted`), `Cancel(actor)`, `GetAcceptedHelperId()`,
    `IsParticipant(user)`, `CreateRating(...)`, `GetOffersVisibleTo(viewer?)`, `StartConversation(...)`, `AllowsConversationWith(helper)`.
  - `Offer` (część agregatu): HelperId, ProposedBy (Client/Helper), Price, ProposedStart/End, Message?, Status, ParentOfferId?,
    AnnouncementRevision (kontroferta = nowy `Offer` wskazujący poprzedni; poprzedni dostaje status `Countered`;
    F11 C/D: autor oczekującej oferty (aktualnej albo nieaktualnej) może ją zmienić, zanim druga strona odpowie –
    `ReviseOffer`, nowa oferta tej samej strony na bieżącej rewizji, poprzednia → `Revised`, zdarzenie `OfferRevised` →
    powiadomienie `OfferRevised` dla drugiej strony).
  - `Image` (część agregatu): FileName (do wyświetlania), FileExtension, FilePath (ścieżka w magazynie, nie trafia do DTO), AddedAt
    (najstarsze = miniatura). Dodanie/usunięcie = nowa rewizja z flagą `Images` (stare/nowe = lista id zdjęć „id1, id2”).
- `Category` (agregat) → `Subcategory` (część agregatu, unikalna nazwa w kategorii).
- `RefreshToken` (agregat, `Entities/Users`): UserId, TokenHash (SHA-256), ExpiresAt, RevokedAt?, ReplacedByTokenId? – `Rotate`, `Revoke`.
- `UserDevice` (agregat, `Entities/Users`) – sekcja „Push i urządzenia”.
- `Rating`: AnnouncementId, RaterId, RateeId, Rate (1–5), Comment?; unikalne (AnnouncementId, RaterId). Ocenianego (druga strona)
  wybiera domena; „raz na ogłoszenie” – handler (`AlreadyRatedException`) + unikalny indeks.
- `LikedAnnouncement` (UserId, AnnouncementId) i `LikedUser` (LikerId, LikedId) – klucze złożone, istnienie wiersza = polubione.
- `Conversation` (agregat, `Entities/Negotiations`): AnnouncementId, ClientId (= właściciel), HelperId, CreatedAt,
  `Client/HelperLastReadAt`, **Messages** (`ConversationMessage`: SenderId, Text (Description ≤ 500), SentAt, AnnouncementRevision).
  Jeden na parę (AnnouncementId, HelperId). Tworzy go `announcement.StartConversation` (tylko Open, nie właściciel);
  `SendMessage(..., announcement, at)` sprawdza `announcement.AllowsConversationWith(HelperId)`: Open – każdy, Accepted/InProgress –
  tylko zaakceptowany pomocnik, Completed/Cancelled – nikt (wątek tylko do odczytu). `MarkAsRead` (znacznik nie cofa się; wysłanie =
  przeczytane), `CountUnread`. Rozmowa ≠ oferta (wiadomości nie da się zaakceptować).
- `AggregateRoot` (baza `Announcement`, `Conversation`): `DomainEvents` (`Core/Events`: `AnnouncementPublished/Edited`,
  `OfferMade/Countered/Revised/Accepted/Rejected/Withdrawn`, `MessageSent`, F11 D: `JobStarted/DoneReported/Completed`),
  czas = parametr metody. `SqlUnitOfWork` przenosi je do outboksa.
- `Notification` (agregat, `Entities/Notifications`): UserId (odbiorca), Type, Payload (JSON parametrów), AnnouncementId?,
  ConversationId?, CreatedAt, ReadAt?, SourceEventId (unikalne z UserId) – sekcja „Powiadomienia”.
- `AnnouncementAlert` (agregat, `Entities/Alerts`): Area (1 punkt) | Route (2–1000), kategoria + podkategorie, RadiusKm 0,5–50,
  dni/godziny, IsEnabled, max 10/użytkownik. Geometria tylko w bazie (`Shape` z EF, `Zone` z triggera SQL) – sekcja „Alerty”.

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
- Fraza (F11 B): `GetAnnouncements.Phrase` → `GetPhrasePattern()` (trim, `%…%`, znaki `%`/`_`/`[`/`\` dosłownie, maks. 100 →
  `InvalidSearchPhraseException` 400) → `EF.Functions.Like((string)(object)a.Title, …)` lub opis (kolumny VO z konwerterem,
  kolacja bez rozróżniania wielkości liter). Bez indeksu pełnotekstowego.

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
- `GET /health` – health check (bez sprawdzania bazy). Porty: http 5080, https 7080. W Development `/` → `/swagger`
  (tylko gdy nie ma frontendu, niżej), `UseHttpsRedirection` tylko poza Development.
- Frontend z API (F10, `Api/Frontend/FrontendExtensions.cs`): gdy w `Frontend:Path` (domyślnie `wwwroot` projektu Api) jest
  `index.html`, API serwuje pliki statyczne (`.webmanifest` → `application/manifest+json`; `index.html`, `ngsw.json`,
  `app-sw.js`, `ngsw-worker.js`, manifest – `no-cache`, reszta 1 dzień) i fallback `{*path:nonfile}` → `index.html` dla adresów
  aplikacji. `/api`, `/hubs`, `/swagger`, `/health` nigdy nie dostają `index.html` – nieznany adres to dalej 404 ProblemDetails.
  `wwwroot` jest w `.gitignore`; kopiuje tam `serve-local.cmd` (build `ICanHelp.Web/www`). Testy: `FrontendHostingTests`
  (fabryka ustawia `Frontend:Path` na katalog tymczasowy z udawanym buildem).
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
- Swagger: przycisk Authorize (Bearer). Przykłady w `ICanHelp.Api.http`. OperationId = `Kontroler_Akcja` (`CustomOperationIds`),
  `SupportNonNullableReferenceTypes` – z tego NSwag generuje klienta frontendu (`UsersClient.login(...)`).
- Refresh tokeny (Krok F2): `sign-in` zwraca też `refreshToken` (+ `refreshTokenExpiresAtUtc`, `Auth:RefreshTokenExpiry`, domyślnie 30 dni).
  Agregat `RefreshToken` (`Core/Entities/Users`, tabela `RefreshTokens`, migracja 20261108200000): w bazie tylko SHA-256 (hex) losowych
  32 bajtów (`Infrastructure/Security/RefreshTokenService`), każde logowanie = osobna sesja. `POST api/users/refresh` (`RefreshSession`,
  anonimowo) – rotacja: stary token `Rotate` → `RevokedAt` + `ReplacedByTokenId`, nowa para w odpowiedzi; `RevokedAt` to token
  współbieżności (dwa równoczesne użycia → 409). Ponowne użycie zrotowanego tokenu: do 1 min (`ReuseGracePeriod`, wyścig kart) – tylko 401,
  później = wyciek → unieważnienie wszystkich aktywnych sesji użytkownika (zapis przed rzuceniem wyjątku). Nieznany/wygasły/unieważniony
  → 401 `InvalidRefreshToken`; zablokowany użytkownik → 403. `POST api/users/sign-out` (`EndSession`, anonimowo, zawsze 204) unieważnia
  podany token. Wspólne wystawianie: `Application/Security/TokenIssuer.IssueAsync`.
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
  `announcementChangedSinceLastRead` (tylko pomocnik), sortowanie po `lastActivityAt`; filtry `role`, `status`, `announcementId`
  (F5: wątki jednego ogłoszenia – szczegóły ogłoszenia), `page`, `pageSize`.
  Jedno zapytanie SQL z podzapytaniami, sort/stronicowanie w pamięci (patrz komentarz w handlerze).
- `api/me/ratings` (F11 F, `MyRatingsController`, query w `Infrastructure/DAL/Handlers/MyRatingsQueryHandlers.cs`): `GET` –
  otrzymane oceny (`MyRatingDto`: ogłoszenie + tytuł, `myRole` = Client/Helper wg właściciela ogłoszenia – `MyRatingDto.RoleOf`,
  `rater`, ocena, komentarz), `GET pending` – „do wystawienia” (`PendingRatingDto`: zakończone ogłoszenia, gdzie czytający jest
  właścicielem albo autorem przyjętej oferty, bez jego oceny; `otherParty`, miniatura). Publiczne `api/users/{id}/ratings` bez zmian.
- Tabele `Conversations` (unikalny indeks AnnouncementId+HelperId, indeksy ClientId/HelperId) i `ConversationMessages` (indeks ConversationId+SentAt),
  Cascade z ogłoszenia. Migracja `Conversations` zakłada wątki dla istniejących ofert.

## Powiadomienia (Krok 13)
- Rura: metoda domenowa → zdarzenie (`Core/Events`, rekordy z samymi Guid/enum/tekst + `OccurredAt` = czas z parametru) w
  `AggregateRoot.DomainEvents` (`Announcement`, `Conversation`) → `SqlUnitOfWork` przed `SaveChanges` przenosi je do `OutboxMessages`
  (ta sama transakcja) → `Infrastructure/Outbox/OutboxProcessor` (pętla `OutboxBackgroundService`, `Outbox:Interval` 2 s, paczki
  `BatchSize`, najstarsze pierwsze, każda wiadomość we własnym scope) woła `IDomainEventHandler<T>` z Application → `ProcessedAt`.
  Błąd → `Attempts++`, `Error`, ponowienie w następnej rundzie do `MaxAttempts` (5). `DomainEvents` ignorowane w modelu EF.
- Zdarzenia: `AnnouncementPublished` (ctor; obsłuży Krok 14), `AnnouncementEdited` (każda nowa rewizja – edycja i zdjęcia; edycja bez zmian
  nic nie podnosi), `OfferMade`, `OfferCountered`, `OfferAccepted`, `OfferRejected` (`AnotherOfferAccepted` = automatycznie przy akceptacji
  innej), `OfferWithdrawn`, `MessageSent`. `AcceptOffer`/`RejectOffer`/`WithdrawOffer` dostały parametr czasu (handler: `IClock`).
- Typ w outboksie = nazwa klasy zdarzenia (`DomainEventSerializer`, JSON, enumy jako tekst) – zmiana nazwy klasy wymaga migracji wierszy.
- `Application/Notifications/TransactionNotificationHandler` – wybór odbiorcy (zawsze druga strona, nigdy autor; edycja → pomocnicy
  z wątkami, `IConversationRepository.GetParticipantsAsync`) i parametry tekstu (`NotificationPayload`: announcementTitle, price,
  messagePreview, changedFields…). `NotificationPublisher` zapisuje (`Notification`, idempotencja: `ExistsAsync` + unikalny indeks
  UserId+SourceEventId, SourceEventId = id wiadomości outboksa) i po commicie wysyła nowe przez `INotificationSender`.
- Kanały: `Api/Notifications/SignalRNotificationSender` + `NotificationsHub` (`/hubs/notifications`, `[Authorize]`, grupa `user:{id}`,
  metoda `notification` z `NotificationDto`; JWT z query `access_token` dla ścieżek `/hubs`). `IEmailSender` → `LoggingEmailSender`
  (tylko log; zbiorczy skrót e-mail – później).
- Api (`NotificationsController`): `GET api/me/notifications` (`unreadOnly`, `page`, `pageSize`, od najnowszych), `GET …/unread-count`,
  `POST …/{id}/read` (cudze/nieistniejące → 404 `NotificationNotFound`), `POST …/read-all` (F6: `?conversationId=` – tylko jeden wątek).
- Frontend (F6): `ICanHelp.Web/src/app/core/notifications/` – `NotificationsService` (SignalR, licznik, toast, `received$`), teksty
  z `type` + `parameters` w `notification-texts.ts`.
- Tabele `Notifications` (FK UserId Restrict; AnnouncementId/ConversationId bez FK – historia; indeksy UserId+SourceEventId unikalny,
  UserId+ReadAt+CreatedAt) i `OutboxMessages` (indeks ProcessedAt+OccurredAt). Migracja `Notifications` (20261108100000, ręczna).
- Testy integracyjne: `Outbox:Enabled=false`, outbox przetwarza `TestApi.ProcessOutboxAsync()` (`IOutboxProcessor`, semafor – nie biegnie
  równolegle z pętlą). Kilka instancji API wymagałoby blokowania wierszy (`UPDLOCK, READPAST`) – na razie niepotrzebne.

## Push i urządzenia (Krok 15)
- `UserDevice` (agregat, `Core/Entities/Users`): Token FCM (unikalny, ≤ 512, bez spacji), Platform (`Web`/`Android`/`Ios`),
  CreatedAt, LastSeenAt; `Refresh` (ten sam token ponownie – także przejęcie przez innego użytkownika), `SelectToRemoveBeforeAdding`
  (limit 10). Tabela `UserDevices` (FK UserId Restrict, indeksy Token unikalny i UserId), migracja `UserDevices` (20261110091546).
- Dostarczanie: handlery → `INotificationSender` = `CompositeNotificationSender` → każdy `INotificationChannel`:
  `SignalRNotificationSender` (Api) i `PushNotificationChannel` (Application: tokeny odbiorcy → `IPushSender` → nieważne tokeny =
  usunięte urządzenia). Cisza nocna alertów działa przed kanałami, więc obejmuje też push.
- `IPushSender`: `Infrastructure/Push/FcmPushSender` (FirebaseAdmin, HTTP v1) gdy `Push:Enabled` i klucz konta serwisowego:
  `Push:Fcm:CredentialsPath` (ścieżka do pliku JSON poza repo) albo `Push:Fcm:CredentialsJson` (treść); inaczej `LoggingPushSender`.
  Lokalnie: user-secrets projektu Api (`dotnet user-secrets set "Push:Fcm:CredentialsPath" "C:\...\icanhelp-firebase.json"`,
  `"Push:Enabled" "true"`), na serwerze – zmienne `Push__Fcm__…`. Klient Firebase powstaje przy pierwszym pushu (`Lazy`): zły klucz
  psuje tylko kanał push (ostrzeżenie w logu), powiadomienia i SignalR działają dalej. Klucz VAPID (Cloud Messaging) to coś innego –
  potrzebuje go tylko frontend (F7). Wiadomość: `notification` (title/body)
  + `data` (notificationId, type, announcementId?, conversationId?, url).
- Teksty: `Application/Notifications/NotificationTextFormatter` – `Title`/`Body`/`Url` w `NotificationDto` (lista, SignalR, push).
- Api: `DevicesController` – `GET/POST api/me/devices`, `DELETE api/me/devices/{id}` (cudze → 404 `DeviceNotFound`).

## Alerty (Krok 14)
- `Core/Entities/Alerts/AnnouncementAlert`: obszar (1 punkt) albo trasa (2–1000 punktów), promień 0,5–50 km, kategoria + podkategorie
  (puste = cała), `ActiveDays`/`ActiveFrom`/`ActiveTo` (czas lokalny, przez północ OK), `IsEnabled`, max 10 na użytkownika.
  Błędne dane → `InvalidAlertException` (400), limit → `DomainRuleException` (409), cudzy alert → `AlertNotFoundException` (404).
- Tabela `AnnouncementAlerts`: `Points`/`SubcategoryIds` jako JSON (`DAL/Converters/AlertConverters.cs`, porównanie po zawartości),
  `Shape geography` (punkt / LINESTRING – ustawia `ICanHelpDbContext.ApplyAlertShapes()` przed zapisem, gdy alert nowy albo zmieniły się
  punkty), `Zone geography` (liczy trigger `TR_AnnouncementAlerts_Zone`: `Shape.MakeValid().Reduce(25).STBuffer(RadiusKm*1000)`,
  EF go nie zapisuje) + `IX_AnnouncementAlerts_Zone` (spatial, w migracji). Trigger zadeklarowany w EF (`HasTrigger`) – bez `OUTPUT`.
- Dopasowanie (`AnnouncementAlertRepository.FindCandidatesAsync`): `IsEnabled AND CategoryId AND UserId <> właściciel AND
  Zone.STIntersects(@punkt) = 1`, odległość `Shape.STDistance(@punkt)` (od środka albo od linii trasy). Podkategorie i harmonogram –
  domena (`Matches`, `IsActiveAt`).
- `Application/Notifications/NewAnnouncementAlertHandler` (`AnnouncementPublished`): tylko ogłoszenie wciąż `Open`; jeden wpis na
  użytkownika (najbliższy alert); `NewAnnouncementNearby` (announcementTitle, categoryName, alertName, alertKind, distanceKm „3.2”);
  powyżej `Alerts:DailyLimit` w dobie lokalnej – jeden `NewAnnouncementsDigest` (count) aktualizowany przez `Notification.Renew`;
  w ciszy nocnej (`Alerts:QuietHours*`) zapis bez `INotificationSender`. Ustawienia: `Application/Alerts/AlertOptions`
  (rejestruje Infrastructure z sekcji `Alerts`).
- Api (`AlertsController`, `api/me/alerts`): GET lista, GET `{id}`, POST (201), PATCH (null = bez zmian; nowa kategoria bez
  podkategorii = cała kategoria; `schedule` zastępuje harmonogram), DELETE, POST `{id}/enable|disable`, GET `{id}/preview?limit=`
  (otwarte ogłoszenia w strefie teraz – `GeoPoint.STIntersects(@zone)`, podkategorie w pamięci z max 500 najnowszych).
