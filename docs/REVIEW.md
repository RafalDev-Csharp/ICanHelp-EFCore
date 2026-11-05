# Code review: Core + Infrastructure/DAL/Configurations (commit 6d683c8 z GitHuba)

> **Status (3.11.2026):** lokalnie Core został już przebudowany (punkty 2–3 w większości rozwiązane),
> a w Kroku 1 przepisano Infrastructure i Application (punkty 1, 4, 5). Dokument zostaje jako historia decyzji.

Podsumowanie: struktura projektów i pomysł z value objects są dobre. Projekt **jeszcze się nie kompiluje**.
Model domeny trzeba uprościć: jest za dużo relacji jeden-do-jednego, konfiguracje EF się dublują
i wzajemnie wykluczają, a negocjacje są rozbite na dwie prawie identyczne klasy.

---

## 1. Błędy kompilacji (do naprawy w pierwszej kolejności)

| Plik | Problem |
|---|---|
| `Core/ValueObjects/Price.cs` | `implicit operator Price(decimal) => new(value)` – brak konstruktora z jednym argumentem. Usuń ten operator (cena bez waluty nie ma sensu). |
| `Core/Repositories/IUserRepository.cs` | `using ICanHelp.Core.Entities.Offers;` – taki namespace nie istnieje. Ma być `Entities.Users`. |
| `Infrastructure/.../RatingConfiguration.cs` | Odwołuje się do `r.User`, `r.UserId`, `r.RatedUser`, a encja ma `Rater`, `RaterId`, `Ratee`, `RateeId`. |
| `Infrastructure/.../AnnouncementRepository.cs` (i pozostałe) | `Repository<T>` wymaga `ICanHelpDbContext` w konstruktorze – klasy pochodne go nie mają. Metody `...Async` nie należą do interfejsu. |
| `Infrastructure/DAL/Extensions.cs` | `services.AddScoped<IContactData, ContactData>()` – `ContactData` to jednocześnie encja i repozytorium (konflikt nazw). |

## 2. Błędy logiczne w Core (skompilują się, ale nie zadziałają)

1. **`Image` konstruktor**: `id = Id;` – przypisanie odwrócone, `Id` zawsze `null`.
2. **`LikedAnnouncement` konstruktor**: `AnnouncementId` przypisane dwa razy, `Announcement` wcale.
3. **`CustomDateTime`**: warunek odwrócony – `value < Now+1rok || value > Now-1dzień` odrzuca **każdą** sensowną datę.
   Dodatkowo walidacja „nie z przeszłości” w VO wysadzi odczyt z bazy (`CreatedAt` sprzed tygodnia).
   Sprawdzanie `Hour 0–23` jest zbędne – `DateTime` tego pilnuje. → Usuń ten VO, używaj `DateTime` (UTC) + walidacji w metodach.
4. **EF nie zbuduje encji**: konstruktory przyjmują nawigacje i kolekcje (`Category category`, `IEnumerable<Image>`…).
   EF potrafi wywołać tylko konstruktor ze zwykłymi właściwościami. → dodaj `private Announcement() { }`.
5. **`Location`**: brak ustawienia `Id`; `double?` w polu, a konstruktor przyjmuje `double`; brak walidacji zakresu (-90..90, -180..180).
6. **`Password`**: przechowujesz hasło jawnie. Domena powinna znać tylko `PasswordHash`; walidacja długości hasła należy do komendy rejestracji.
7. **`Rate`**: `class` zamiast `record`, `double` (gwiazdki 4.37?) → `sealed record Rating(int Stars)` lub `int` 1–5.
8. **Wyjątki `internal`**: warstwa API nie złapie np. `InvalidEmailException`. → `public`, nazwa bazowa w liczbie pojedynczej: `CustomException`.
   Część VO rzuca `ArgumentException` zamiast własnych – ujednolicić.
9. `Email`/`PhoneNumber`: `Regex` jako pole instancji – kompilowany przy każdym obiekcie. → `private static readonly`.

## 3. Problemy z modelem domeny (najważniejsze dla rozwoju)

### a) Role
`Role` (VO) i `AppUserRole` (encja) istnieją jednocześnie. `AppUserRole` ma relację 1:1 z `User` (każdy user ma *własny* wiersz roli),
a do tego trzyma `ClientOffers`/`HelperOffers`. Klient/pomocnik to **rola w transakcji**, nie typ konta.
→ `User.Role` jako enum `UserRole { User, Admin }`. Usuń `AppUserRole`. W ofercie wystarczy `HelperId`, a klient = `Announcement.OwnerId`.

### b) Negocjacje
`ClientOffer` i `HelperOffer` to prawie ten sam kod; ogłoszenie ma 1 `ClientOffer` i N `HelperOffers`; ocena jest przypięta do oferty.
→ Jedna encja **`Offer`** (oferta pomocnika na ogłoszenie) + lista **`OfferProposal`** (kolejne rundy negocjacji, `ProposedBy: Client|Helper`).
Akceptacja przez metodę agregatu:

```csharp
public sealed class Announcement
{
    private readonly List<Offer> _offers = new();
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public AnnouncementStatus Status { get; private set; }
    public IReadOnlyCollection<Offer> Offers => _offers;

    private Announcement() { } // EF

    public Offer PlaceOffer(Guid helperId, Price price, string message, DateTime now)
    {
        if (helperId == OwnerId) throw new CannotOfferOwnAnnouncementException();
        if (Status is not (AnnouncementStatus.Open or AnnouncementStatus.InNegotiation))
            throw new AnnouncementNotOpenException(Id);
        if (_offers.Any(o => o.HelperId == helperId && o.Status == OfferStatus.Pending))
            throw new OfferAlreadyExistsException();

        var offer = Offer.Create(Id, helperId, price, message, now);
        _offers.Add(offer);
        Status = AnnouncementStatus.InNegotiation;
        return offer;
    }

    public void AcceptOffer(Guid offerId, Guid requestedBy)
    {
        if (requestedBy != OwnerId) throw new NotAnnouncementOwnerException();
        var offer = _offers.SingleOrDefault(o => o.Id == offerId) ?? throw new OfferNotFoundException(offerId);
        offer.Accept();
        foreach (var other in _offers.Where(o => o.Id != offerId && o.Status == OfferStatus.Pending))
            other.Reject();
        Status = AnnouncementStatus.Assigned;
    }
}
```

To jest dokładnie ten kod, który warto pokazać na rozmowie i przykryć testami jednostkowymi.

### c) Statusy zamiast booli
`IsAvailable` + `IsFinished` daje 4 kombinacje, z czego jedna jest bez sensu. → `AnnouncementStatus { Open, InNegotiation, Assigned, Completed, Cancelled }`.

### d) Polubienia
`Announcement.LikedAnnouncementId` + relacja 1:1 = ogłoszenie może polubić tylko jedna osoba. `IsLiked` jest zbędne (istnienie wiersza = polubione).
Klasa bazowa `Liked` wymusza dziedziczenie w EF (TPH), którego nie skonfigurowano.
→ `FavoriteAnnouncement(UserId, AnnouncementId)` z kluczem złożonym; osobno `FavoriteHelper(UserId, HelperId)` – albo na razie wcale (to nie jest MVP).

### e) Lokalizacja
`Location` jako osobna tabela 1:1 z FK po obu stronach (`Announcement.LocationId` i `Location.AnnouncementId`).
→ `GeoLocation` jako **value object (owned type)** w tabeli ogłoszeń. Później: `HelperProfile` z punktem trasy/obszarem + promień,
zapytania odległości przez NetTopologySuite (`geography` w SQL Server) – mocny punkt w CV.

### f) Pozostałe
- `Announcement` ma `CategoryId` **i** `SubcategoryId` – kategoria wynika z podkategorii; mogą się rozjechać. Zostaw samo `SubcategoryId`.
- `DifficultLevel` jako tabela → enum `Difficulty { Easy, Medium, Hard }` (i poprawna nazwa *Difficulty*).
- `ContactData` 1:1 z `User` z FK po obu stronach → wciągnij pola do `User` (lub owned type). Email jako login powinien być w `User`.
- `Rating` z `ClientOfferId` i `HelperOfferId` → `Rating(AnnouncementId, RaterId, RateeId, Stars, Comment)`, unikat `(AnnouncementId, RaterId)`.
- Publiczne settery wszędzie = model anemiczny; metody `UpdateX(...)` z 9 parametrami to w praktyce settery. Lepiej małe, nazwane operacje: `ChangeBudget`, `Cancel`, `Complete`.
- Dwa typy ID (`Id`, `UserId`) – dla juniora wystarczy `Guid`. Jeżeli chcesz silne typy, to per encja (`AnnouncementId`, `OfferId`…), inaczej `Id` nic nie wnosi.

## 4. Konfiguracje EF (Infrastructure/DAL/Configurations)

1. **Brak konwerterów dla `Id`/`UserId`** – poza `User.Id`. EF nie zmapuje kluczy. Rozwiązanie globalne:
   ```csharp
   protected override void ConfigureConventions(ModelConfigurationBuilder b)
   {
       b.Properties<Id>().HaveConversion<IdConverter>();       // ValueConverter<Id, Guid>
       b.Properties<UserId>().HaveConversion<UserIdConverter>();
   }
   ```
   (lub przejście na `Guid` – patrz 3f).
2. **`Price` (dwie właściwości)** mapowane przez `Property(x => x.Price)` – wyjątek przy budowie modelu. → `builder.ComplexProperty(x => x.Price)` lub `OwnsOne`, z `HasPrecision(18,2)` dla kwoty.
3. **Relacje konfigurowane podwójnie i sprzecznie**:
   - Location: w `AnnouncementConfiguration` FK = `Location.Id`, w `LocationConfiguration` FK = `Location.AnnouncementId`.
   - Category↔Announcement, Category↔Subcategory, DifficultLevel, Images, ContactData, Announcements – w obu plikach.
   → Każda relacja **raz**, w konfiguracji encji zależnej.
4. **Złe klucze obce**:
   - `ClientOfferConfiguration`: `Helper` z FK `ClientId` i `WithMany(ClientOffers)` (skopiowane z Client).
   - `HelperOfferConfiguration`: `Client` z FK `HelperId`.
   - `LikedAnnouncementConfiguration`: `UserLiker` z FK `AnnouncementId` (ma być `UserLikerId`), a `UserConfiguration` mapuje tę samą relację z innym FK.
   - `LikedUserConfiguration`: `UserLiker` i `UserLiked` wskazują na tę samą kolekcję `LikedUsers` – EF rzuci wyjątek.
   - `UserConfiguration`: `ClientRatings` i `HelperRatings` z tym samym FK `UserId` (nie istnieje).
   - Offer: `AcceptedById`/`CreatedById` typu `UserId`, a nawigacja do `AppUserRole` (klucz `Id`) – niezgodne typy.
5. **Wiele ścieżek kaskadowych** (User → Announcement → Offer i User → Offer, oba `Cascade`) – SQL Server odrzuci migrację.
   → `Restrict` dla relacji do `User`, `Cascade` tylko dla Announcement → Offer/Image.
6. **Brakujące konwersje**: `Description` w obu konfiguracjach ofert, `CreatedAt` w `AnnouncementConfiguration` (typ `CustomDateTime`), wszystkie pola `Id`/`UserId` poza `User.Id`.
7. **Niespójne limity**: `Description` VO max 500 vs kolumna 1000; `PhoneNumber` VO do 25 znaków vs kolumna 15. Trzymaj stałe w jednym miejscu (np. `Description.MaxLength`).
8. Brak indeksów pod typowe zapytania: `Announcement(Status, CreatedAt)`, `Offer(AnnouncementId, HelperId)`, unikat `User.UserName`/`Email`.
9. Drobiazgi: `using Microsoft.Identity.Client` (zbędny), komentarze z poprzedniej sesji AI („Gotchas to check”) w kodzie produkcyjnym.

## 5. Inne uwagi (Infrastructure)
- `Extensions.AddSql`: `AddDbContext` wywołane dwa razy (dwa różne connection stringi), `IUserRepository` zarejestrowane dwa razy, brak `IAnnouncementRepository`.
- Swashbuckle w Infrastructure – należy do projektu Api.
- `Repository<T>`: synchroniczne `ToList()`/`FirstOrDefault()`, include'y jako string. → async + repozytoria per agregat
  (`IAnnouncementRepository.GetByIdAsync(id, ct)` ładujące `Offers`). Zapytania (Queries) mogą iść bezpośrednio przez `DbContext` z `AsNoTracking` + projekcją do DTO.
- `SqlUnitOfWork` – dobry pomysł, zostaw.
- Brak projektu Api, testów, migracji, README.

## 6. Rekomendowana kolejność
1. Napraw błędy kompilacji (sekcja 1) – szybki sukces, `dotnet build` zielony.
2. Przebuduj Core wg `CLAUDE.md` → *Docelowy model domeny*. Zacznij od `Announcement` + `Offer` + testy xUnit dla `PlaceOffer`/`AcceptOffer`.
3. Usuń obecne konfiguracje i napisz je od nowa dla nowego modelu (będzie ich ~6 zamiast 13), pierwsza migracja.
4. Api + Swagger + middleware wyjątków → pierwszy działający endpoint „dodaj ogłoszenie”.
