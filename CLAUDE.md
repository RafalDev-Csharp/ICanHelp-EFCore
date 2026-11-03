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
- Clean Architecture: `Core` (domena) ← `Application` (CQRS: komendy/zapytania, DTO) ← `Infrastructure` (EF, repozytoria) ← `Api` (do utworzenia)
- Testy: xUnit + Shouldly (`ICanHelp.Core.Tests` jest; do utworzenia: `ICanHelp.Application.Tests`).
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
   W Application czas da `IClock` (do dodania w Kroku 6). Nie waliduj „przeszłości” w VO – waliduj w metodzie domenowej.
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
Application     Abstractions (ICommand/IQuery + handlery), DTO, Queries, (Commands – Krok 6), Extensions.AddApplication()
Infrastructure  DAL/ICanHelpDbContext, DAL/Converters, DAL/Configurations, DAL/Repositories,
                DAL/Handlers (query handlery + mapowanie AsDto), SqlUnitOfWork, DbInitializer, Extensions.AddInfrastructure()
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

## Komendy
```bash
dotnet build
dotnet test
# komendy ef zadziałają po utworzeniu projektu Api (Krok 3), migracja Init w Kroku 4
dotnet ef migrations add <Name> -p ICanHelp.Infrastructure -s ICanHelp.Api
dotnet ef database update      -p ICanHelp.Infrastructure -s ICanHelp.Api
```

## Stan prac (aktualizuj!)
- [x] Szkielet: Core, Application (DTO, Queries), Infrastructure
- [x] Przebudowa domeny (Core) – model powyżej
- [x] Krok 1 – Infrastructure + Application dopasowane do nowego Core (konfiguracje EF, repozytoria, query handlery)
- [ ] Krok 1b – `dotnet build` zielony u autora, usunięcie plików z komentarzem `// OBSOLETE`
- [x] Krok 2 – projekt `ICanHelp.Core.Tests` (xUnit + Shouldly): testy `Announcement` (oferty, kontroferty, akceptacja, statusy)
  – `Entities/AnnouncementTests.cs` (tworzenie, edycja, zdjęcia, cykl życia), `Entities/AnnouncementOfferTests.cs` (negocjacje),
  `Entities/AnnouncementTestData.cs` (stały czas `Now`, użytkownicy, `InStatus(...)`). `dotnet test`: 77/77 zielone.
- [ ] Krok 3 – projekt `ICanHelp.Api` (Controllers), Swagger, obsługa wyjątków (ProblemDetails), appsettings
- [ ] Krok 4 – pierwsza migracja `Init` + Docker Compose z SQL Server
- [ ] Krok 5 – rejestracja/logowanie (JWT, hash hasła)
- [ ] Krok 6 – use-case'y: dodaj ogłoszenie, złóż ofertę, kontroferta, akceptacja, zakończenie, ocena
- [ ] Krok 7 – wyszukiwanie po odległości (NetTopologySuite / geography). Paginacja i filtry listy już są (`GetAnnouncements`).
- [ ] Krok 8 – testy integracyjne (Testcontainers), GitHub Actions CI, README z diagramem

## Jak pracować z AI (oszczędnie)
- Jedna sesja = jeden krok z listy. Na start: „Przeczytaj CLAUDE.md, zrób Krok N”.
- Podawaj konkretne pliki, zamiast prosić o przeszukanie repo.
- Po kroku: zaktualizuj „Stan prac”, commit.
