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
- Testy: xUnit + FluentAssertions (do utworzenia: `ICanHelp.Core.Tests`, `ICanHelp.Application.Tests`)

## Zasady w kodzie (stosuj zawsze)
1. **Core nie zależy od niczego** (żadnego EF, ASP.NET). Tylko czysty C#.
2. Encje: `private set`, prywatny bezparametrowy konstruktor dla EF, publiczna metoda fabryczna `Create(...)`
   albo konstruktor przyjmujący **tylko wartości** (nigdy nawigacje ani kolekcje).
3. Kolekcje: `private readonly List<T> _items = new(); public IReadOnlyCollection<T> Items => _items;`
4. Logika biznesowa w encjach (np. `announcement.AcceptOffer(offerId)`), nie w handlerach.
5. Value objects jako `sealed record`, walidacja w konstruktorze, wyjątek dziedziczący po `CustomException` (public).
6. Daty: `DateTimeOffset`/`DateTime` UTC, czas z `IClock` (testowalność). Nie waliduj „przeszłości” w VO – waliduj w metodzie domenowej.
7. Status zamiast wielu booli (`AnnouncementStatus`, `OfferStatus`).
8. Konfiguracja EF: każdą relację konfiguruj **raz** (relacje wewnątrz agregatu – w konfiguracji korzenia agregatu).
   Konwertery VO globalnie w `ConfigureConventions`. VO z kilkoma polami jako `ComplexProperty`/`OwnsOne`.
9. Na SQL Server unikaj wielu ścieżek kaskadowych – domyślnie `DeleteBehavior.Restrict`, `Cascade` tylko dla części agregatu.
10. Async wszędzie (`Task`, `CancellationToken`), repozytorium per agregat, `SaveChanges` przez `IUnitOfWork`.
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
Po akceptacji jednej oferty pozostałe `Pending` → `Rejected`.

## Infrastructure (jak jest zbudowane)
- `ICanHelpDbContext.ConfigureConventions` – globalne konwertery VO (`DAL/Converters`) i enumy jako string.
- `Price`, `Location` → `ComplexProperty`; `ContactData` → `OwnsOne` (indeks na Email).
- Relacje między agregatami tylko przez FK (`HasOne<User>().WithMany()`), bez nawigacji. Cascade tylko wewnątrz agregatu.
- Repozytoria per agregat (`DAL/Repositories`) – ładują cały agregat (`Include` Offers/Images).
- Strona odczytu: handlery zapytań w `DAL/Handlers` czytają z DbContext (`AsNoTracking`) i mapują do DTO (`Handlers/Extensions.cs`).
- `DbInitializer` (IHostedService) – migracje + seed kategorii przy starcie.

## Komendy
```bash
dotnet build
dotnet test
dotnet ef migrations add <Name> -p ICanHelp.Infrastructure -s ICanHelp.Api
dotnet ef database update      -p ICanHelp.Infrastructure -s ICanHelp.Api
```

## Stan prac (aktualizuj!)
- [x] Szkielet: Core, Application (DTO, Queries), Infrastructure
- [x] Przebudowa domeny (Core) – model powyżej
- [x] Krok 1 – Infrastructure + Application dopasowane do nowego Core (konfiguracje EF, repozytoria, query handlery)
- [ ] Krok 1b – `dotnet build` zielony u autora, usunięcie plików z komentarzem `// OBSOLETE`
- [ ] Krok 2 – projekt `ICanHelp.Core.Tests` (xUnit): testy `Announcement` (oferty, kontroferty, akceptacja, statusy)
- [ ] Krok 3 – pierwsza migracja `Init` + Docker Compose z SQL Server
- [ ] Krok 4 – projekt `ICanHelp.Api` (Minimal API/Controllers), Swagger, obsługa wyjątków (ProblemDetails)
- [ ] Krok 5 – rejestracja/logowanie (JWT, hash hasła)
- [ ] Krok 6 – use-case'y: dodaj ogłoszenie, złóż ofertę, kontroferta, akceptacja, zakończenie, ocena
- [ ] Krok 7 – wyszukiwanie po odległości (NetTopologySuite / geography), filtrowanie, paginacja
- [ ] Krok 8 – testy integracyjne (Testcontainers), GitHub Actions CI, README z diagramem

## Jak pracować z AI (oszczędnie)
- Jedna sesja = jeden krok z listy. Na start: „Przeczytaj CLAUDE.md, zrób Krok N”.
- Podawaj konkretne pliki, zamiast prosić o przeszukanie repo.
- Po kroku: zaktualizuj „Stan prac”, commit.
