# Krok 1 – co się zmieniło i dlaczego

**W skrócie:** domena (Core) została wcześniej przebudowana, a Infrastructure i Application nadal mówiły o starych
klasach (`ClientOffer`, `HelperOffer`, `AppUserRole`, `DifficultLevel`, encja `Location`…). Krok 1 dopasował obie
warstwy do nowego Core. Projekt ma się znowu kompilować, a baza ma dać się utworzyć migracją.

Legenda: `+` nowy plik · `~` przepisany · `x` do usunięcia (plik z komentarzem `// OBSOLETE`) · `=` bez zmian

---

## 1. Struktura plików

```
ICanHelpApp
├── CLAUDE.md                                  +  kontekst dla AI (czyta go na start sesji)
├── docs/REVIEW.md, docs/KROK-1-ZMIANY.md      +
│
├── ICanHelp.Core                              =  (przebudowany wcześniej, w Kroku 1 nietknięty)
│   ├── Entities/  Announcement, Offer, Category, Subcategory, Image, Rating, User, Liked*
│   ├── ValueObjects/  Id, UserId, Price, Location, Email, ...
│   ├── Enums/  AnnouncementStatus, OfferStatus, OfferParty, DifficultyLevel, UserRole
│   └── Repositories/  I*Repository, IUnitOfWork   ◄── jedyne IUnitOfWork w solucji
│
├── ICanHelp.Application
│   ├── Abstractions/  ICommand, IQuery, I*Handler   ~  + CancellationToken
│   ├── Extensions.cs                                +  AddApplication() – rejestracja handlerów komend
│   ├── DTO/
│   │   ├── AnnouncementDto (+ AnnouncementDetailsDto) ~  płaskie, bez zagnieżdżonych encji
│   │   ├── OfferDto, PriceDto                       +
│   │   ├── Category/Subcategory/User/ContactData/Rating/Image/Location/Liked*Dto  ~
│   │   └── AppUserRoleDto, ClientOfferDto, HelperOfferDto, DifficultLevelDto  x
│   └── Queries/
│       ├── GetAnnouncements (filtry + stronicowanie), GetAnnouncementById,
│       │   GetCategories, GetCategoryById, GetUsers, GetUserById,
│       │   GetRatingByAnnouncementId, GetLikedAnnouncements, GetLikedUsers   ~
│       ├── GetRatingsByUser                         +
│       └── 16 starych (ClientOffer*, HelperOffer*, Location*, Image*, Subcategory*, ...)  x
│
└── ICanHelp.Infrastructure
    ├── ICanHelp.Infrastructure.csproj   ~  bez Swaggera (przejdzie do Api), + Hosting/Configuration
    ├── Extensions.cs                    ~  AddInfrastructure() – jedno wejście dla Api
    ├── DbInitializer.cs                 ~  był kodem z kursu (Identity) → migracje + seed kategorii
    └── DAL/
        ├── ICanHelpDbContext.cs         ~  + ConfigureConventions (konwertery w jednym miejscu)
        ├── Converters/                  +  Id→Guid, Email→string, Rate→int, ...
        ├── Configurations/
        │   ├── Announcement, Category, Subcategory, Image, User, Rating, Liked*  ~
        │   ├── OfferConfiguration                                               +
        │   └── AppUserRole, ClientOffer, HelperOffer, ContactData, DifficultLevel, Location  x
        ├── Repositories/
        │   ├── Announcement, Category, User, Rating, LikedAnnouncement, LikedUser  ~  async, per agregat
        │   └── AppUserRole, ContactData, DifficultLevel, Images, Location, Subcategory  x
        ├── Handlers/  Get*Handler (10 szt.)  +   Extensions.cs (AsDto)  ~
        ├── SqlUnitOfWork.cs             ~  implementuje Core.IUnitOfWork (+ CancellationToken)
        ├── IUnitOfWork.cs               x  duplikat interfejsu z Core
        └── Repository.cs                x  generyczne repozytorium
```

## 2. Model bazy – przed i po

```
PRZED (13 tabel, dużo 1:1 i podwójnych relacji)        PO (9 tabel)

Users ──1:1── AppUserRoles                              Users  (+ Email, Phone, City... w tej samej tabeli,
Users ──1:1── ContactData                                       Role jako tekst "User"/"Admin")
Announcements ──1:1── Locations                          Announcements (+ Latitude, Longitude,
Announcements ──1:1── ClientOffers                                      BudgetAmount, BudgetCurrency)
Announcements ──1:N── HelperOffers                        ├──1:N── Offers  (jedna tabela, ParentOfferId = kontroferta)
Announcements ──1:1── LikedAnnouncements  (!)             └──1:N── Images
Announcements ──N:1── DifficultLevels                    Categories ──1:N── Subcategories
Ratings → ClientOffer + HelperOffer                      Ratings (RaterId, RateeId, unikat AnnouncementId+RaterId)
                                                         LikedAnnouncements (klucz: UserId+AnnouncementId)
                                                         LikedUsers         (klucz: LikerId+LikedId)
```

Dlaczego tak:
- **Lokalizacja, cena i dane kontaktowe** nie mają własnej tożsamości, to tylko wartości. Trzymanie ich
  w osobnych tabelach 1:1 dawało joiny bez żadnego zysku. Teraz to kolumny w tabeli właściciela
  (`ComplexProperty` / `OwnsOne`).
- **Rola, poziom trudności** to stała, krótka lista, więc wystarczy enum zapisany jako tekst, bez tabeli i joina.
- **Jedna tabela `Offers`**: oferta i kontroferta to ta sama rzecz, różni je tylko `ProposedBy`.
- **Polubienia** miały 1:1 (ogłoszenie mogła polubić jedna osoba). Teraz klucz złożony = każdy może polubić raz.

## 3. Jak płyną dane (CQRS: zapis ≠ odczyt)

```
 ZAPIS (komendy – Krok 6)                              ODCZYT (zapytania – gotowe w Kroku 1)

 Api: POST /announcements/{id}/offers                  Api: GET /announcements?status=Open&page=1
        │                                                     │
        ▼                                                     ▼
 Application: AddOfferHandler                          Infrastructure: GetAnnouncementsHandler
        │  repo.GetByIdAsync(id)  ──► cały agregat           │  DbContext.Announcements
        │                             (Offers + Images)       │    .AsNoTracking()   ◄── bez śledzenia, szybciej
        ▼                                                     │    .Where(...).Skip().Take()
 Core: announcement.AddOffer(...)                             ▼
        │  ◄── tu są reguły: status Open?                AsDto()  ──► AnnouncementDto (same proste typy)
        │      właściciel nie składa oferty sobie? ...        │
        ▼                                                     ▼
 IUnitOfWork.ExecuteAsync  ──► transakcja + SaveChanges    JSON do klienta
```

Dlaczego tak:
- **Zapis przez agregat**: reguły („tylko druga strona może zaakceptować”, „po akceptacji reszta odrzucona”)
  są w jednym miejscu, w `Announcement`. Repozytorium tylko ładuje i dodaje obiekty, nie decyduje.
- **Odczyt z pominięciem domeny**: lista ogłoszeń nie potrzebuje reguł, więc handler czyta wprost z DbContext.
  Dlatego handlery zapytań są w Infrastructure, a handlery komend będą w Application.
- **`IUnitOfWork` w Core**: Application zależy tylko od Core, więc interfejs musi tam być.
  Kopia w `Infrastructure/DAL` była duplikatem i dlatego jest do usunięcia.

## 4. Najważniejsze decyzje w konfiguracji EF

| Co | Dlaczego |
|---|---|
| `ConfigureConventions` z konwerterami VO | Wcześniej `HasConversion` było powtarzane w każdym pliku, a w wielu miejscach go brakowało (Id). Teraz jest jedno miejsce. |
| Enumy jako `string` | W bazie widać `Open`, a nie `1`. Łatwiej czytać i debugować. |
| Relacje między agregatami tylko przez FK (`HasOne<User>().WithMany()`) | Agregaty nie trzymają się nawzajem w pamięci, więc nie da się „przez przypadek” zmienić usera z poziomu ogłoszenia. |
| `Cascade` tylko wewnątrz agregatu, gdzie indziej `Restrict` | SQL Server odrzuca wiele ścieżek kaskadowego usuwania. Poprzednia konfiguracja nie przeszłaby migracji. |
| Każda relacja skonfigurowana **raz** | Wcześniej ta sama relacja była w dwóch plikach, czasem z różnymi kluczami obcymi. |
| Indeksy: `Status+CreatedAt`, unikalne `UserName`, `Email`, `AnnouncementId+RaterId` | Pod typowe zapytania i reguły unikalności. |
| `ValueGeneratedNever()` dla kluczy | Id tworzy domena (`Id.New()`), nie baza. |

## 5. Co jeszcze poprawiono w CLAUDE.md po Kroku 1

- `IUnitOfWork`: doprecyzowano, że interfejs jest w Core, a repozytoria nie wołają `SaveChanges`.
- `IClock` jeszcze nie istnieje. Domena dostaje czas parametrem (`createdAtUtc`).
- Nazwa wyjątku bazowego: `CustomExceptions` (+ `DomainRuleException`).
- Fabryki `Create()` nie ma. Są konstruktory, a części agregatu mają konstruktor `internal`.
- Dodana sekcja „Gdzie co leży”.
- Zamiana kolejności kroków: najpierw Api (Krok 3), potem migracja (Krok 4), bo `dotnet ef` potrzebuje projektu startowego.
- Paginacja już jest, więc zniknęła z Kroku 7.
- Testy: Shouldly zamiast FluentAssertions (wersja 8+ ma płatną licencję komercyjną).
