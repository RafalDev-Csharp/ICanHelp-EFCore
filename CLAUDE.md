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
- Testy: xUnit + Shouldly (`ICanHelp.Core.Tests`, `ICanHelp.Application.Tests` – handlery z ręcznymi fake'ami w `Fakes/`).
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
  Location (VO lat/lon), Status, CreatedAt, AcceptedOfferId?, **Offers**, **Images** (max 5)
  - metody: `AddOffer`, `CounterOffer`, `AcceptOffer`, `RejectOffer`, `WithdrawOffer`, `AddImage`, `RemoveImage`, `Start`, `Complete`, `Cancel`
  - `Offer` (część agregatu): HelperId, ProposedBy (Client/Helper), Price, ProposedStart/End, Message?, Status, ParentOfferId?
    (kontroferta = nowy `Offer` wskazujący poprzedni; poprzedni dostaje status `Countered`)
- `Category` (agregat) → `Subcategory` (część agregatu, unikalna nazwa w kategorii)
- `Rating`: AnnouncementId, RaterId, RateeId, Rate (1–5), Comment?; unikalne (AnnouncementId, RaterId)
- `LikedAnnouncement` (UserId, AnnouncementId) i `LikedUser` (LikerId, LikedId) – klucze złożone, istnienie wiersza = polubione
- Później: `HelperProfile` z obszarem działania / trasą dojazdu (punkt + promień) → wyszukiwanie „po drodze”

## Przepływ statusów
Announcement: `Open` → (AcceptOffer) `Accepted` → (Start) `InProgress` → (Complete) `Completed`; `Open`/`Accepted` → `Cancelled`
Offer: `Pending` → `Accepted` | `Rejected` | `Withdrawn` | `Countered` (zastąpiona kontrofertą)
Akceptuje/odrzuca/kontruje zawsze **druga strona** niż autor oferty; wycofać może tylko autor.
Oferty przyjmowane tylko w statusie `Open`. Po akceptacji pozostałe `Pending` → `Rejected`.

## Gdzie co leży
```
Core            Entities, ValueObjects, Enums, Exceptions, Repositories (interfejsy + IUnitOfWork)
Application     Abstractions (ICommand/IQuery + handlery), DTO, Queries, Commands (+ Commands/Handlers), Exceptions,
                Security (IPasswordManager, IAuthenticator, ITokenStorage), Time (IClock), Extensions.AddApplication()
Infrastructure  DAL/ICanHelpDbContext, DAL/Converters, DAL/Configurations, DAL/Repositories,
                DAL/Handlers (query handlery + mapowanie AsDto), SqlUnitOfWork, DbInitializer, Security (JWT, hasła), Time,
                Extensions.AddInfrastructure()
Api             Program.cs, Controllers (Announcements, Categories, Users), ErrorHandling/DomainExceptionHandler,
                appsettings*.json, Properties/launchSettings.json, ICanHelp.Api.http (przykładowe żądania)
```
Zapytania (`GetX`) definiuje Application, a ich handlery są w Infrastructure, bo czytają prosto z DbContext.
Handlery komend będą w Application i będą używać repozytoriów + `IUnitOfWork`. Rejestracja handlerów: Scrutor.

## Infrastructure (jak jest zbudowane)
- `ICanHelpDbContext.ConfigureConventions` – globalne konwertery VO (`DAL/Converters`) i enumy jako string.
- `Price`, `Location` → `ComplexProperty`; `ContactData` → `OwnsOne` (indeks na Email).
- Relacje między agregatami tylko przez FK (`HasOne<User>().WithMany()`), bez nawigacji. Cascade tylko wewnątrz agregatu.
- Repozytoria per agregat (`DAL/Repositories`) – ładują cały agregat (`Include` Offers/Images).
- Strona odczytu: handlery zapytań w `DAL/Handlers` czytają z DbContext (`AsNoTracking`) i mapują do DTO (`Handlers/Extensions.cs`).
- `DbInitializer` (IHostedService) – przy starcie: migracje + seed kategorii (nic nie robi, dopóki nie ma żadnej migracji).
- Connection string: `ConnectionStrings:DefaultConnection` (w appsettings projektu Api).
- Migracje w `ICanHelp.Infrastructure/DAL/Migrations`. `dotnet-ef` jako lokalne narzędzie (`.config/dotnet-tools.json`, 9.0.11).

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
  Krok 6: kontakt drugiej strony dopiero po akceptacji oferty; `OwnerId`/`HelperId` w komendach brać z tokenu (`sub`), nie z body.

## Api (jak jest zbudowane)
- Kontrolery `[ApiController]`, trasy `api/<zasób>`. Handler wstrzykiwany do akcji przez `[FromServices] IQueryHandler<TQuery, TResult>`
  (bez mediatora). Zapytanie z filtrami bindowane `[FromQuery]` prosto do klasy z Application (`GetAnnouncements`, `GetUsers`).
  `null` z handlera → `NotFound()`. Komendy: `[FromBody]` + `[FromServices] ICommandHandler<T>`. Dostęp do GET-ów – sekcja „Uwierzytelnianie”.
- Błędy zawsze jako ProblemDetails: `AddProblemDetails` + `UseExceptionHandler` + `UseStatusCodePages`.
  `DomainExceptionHandler`: `OfferNotFoundException` → 404, `DomainRuleException` → 409, wyjątki auth (wyżej), inne `CustomExceptions` → 400
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
dotnet test
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
- [ ] Krok 1b – ~~`dotnet build` zielony u autora~~ (zrobione), zostało: usunąć `DAL/Configurations/LocationConfiguration.cs` (`// OBSOLETE`)
- [x] Krok 2 – projekt `ICanHelp.Core.Tests` (xUnit + Shouldly): testy `Announcement` (oferty, kontroferty, akceptacja, statusy)
  – `Entities/AnnouncementTests.cs` (tworzenie, edycja, zdjęcia, cykl życia), `Entities/AnnouncementOfferTests.cs` (negocjacje),
  `Entities/AnnouncementTestData.cs` (stały czas `Now`, użytkownicy, `InStatus(...)`). `dotnet test`: 77/77 zielone.
- [x] Krok 3 – projekt `ICanHelp.Api`: kontrolery GET dla wszystkich zapytań, Swagger, ProblemDetails (`DomainExceptionHandler`),
  appsettings, launchSettings, `/health`. `dotnet build` zielony u autora (3.11.2026).
- [x] Krok 4 – `docker-compose.yml` (SQL Server 2022), connection string, manifest `dotnet-ef`,
  migracja `Init` w `DAL/Migrations` (20261105141305). API startuje, `DbInitializer` migruje i seeduje kategorie.
- [x] Krok 5 – rejestracja/logowanie (JWT, hash hasła): opis w sekcji „Uwierzytelnianie”, projekt `ICanHelp.Application.Tests`
  (14 testów `SignUpHandler`/`SignInHandler`). Bez nowej migracji (schemat Users bez zmian). **Do sprawdzenia u autora: `dotnet build` + `dotnet test`.**
- [ ] Krok 6 – use-case'y: dodaj ogłoszenie, złóż ofertę, kontroferta, akceptacja, zakończenie, ocena
- [ ] Krok 7 – wyszukiwanie po odległości (NetTopologySuite / geography). Paginacja i filtry listy już są (`GetAnnouncements`).
- [ ] Krok 8 – testy integracyjne (Testcontainers), GitHub Actions CI, README z diagramem

## Jak pracować z AI (oszczędnie)
- Jedna sesja = jeden krok z listy. Na start: „Przeczytaj CLAUDE.md, zrób Krok N”.
- Podawaj konkretne pliki, zamiast prosić o przeszukanie repo.
- Po kroku: zaktualizuj „Stan prac”, commit.
